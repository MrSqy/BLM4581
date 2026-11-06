// Test URL'si SQLite + taklit runner kullanan izole test sunucusu olmalıdır.
// PLAYWRIGHT_MODULE_PATH isteğe bağlıdır; üretim uygulamasına bağımlılık eklemez.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE_PATH || "playwright");
const assert = require("node:assert/strict");
const path = require("node:path");

(async () => {
    const browser = await chromium.launch({ headless: true, ...(process.env.EDITOR_BROWSER_PATH ? { executablePath: process.env.EDITOR_BROWSER_PATH } : {}) });
    try {
        const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
        const base = process.env.EDITOR_TEST_URL;
        assert.ok(base, "EDITOR_TEST_URL izole test sunucusuna ayarlanmalı.");
        const errors = [];
        const consoleErrors = [];
        page.on("pageerror", error => errors.push(error.message));
        page.on("console", message => { if (message.type() === "error") consoleErrors.push(message.text()); });
        await page.goto(`${base}/Egitimler/Detay/1`);
        await page.waitForFunction(() => document.getElementById("editor-load-status").textContent === "Monaco editörü hazır.");
        const code = () => page.evaluate(async () => {
            const config = JSON.parse(document.getElementById("code-editor-config").textContent);
            const { monaco } = await import(config.monacoUrl);
            return monaco.editor.getModels().find(model => !model.isDisposed()).getValue();
        });
        const setCode = value => page.evaluate(async value => {
            const config = JSON.parse(document.getElementById("code-editor-config").textContent);
            const { monaco } = await import(config.monacoUrl);
            monaco.editor.getEditors()[0].getModel().setValue(value);
        }, value);
        const baseline = await page.evaluate(() => JSON.parse(document.getElementById("code-editor-config").textContent).gorevler[0].baslangicKodu);
        assert.equal(await code(), baseline.replace(/\r\n/g, "\n"));
        const language = await page.evaluate(async () => {
            const config = JSON.parse(document.getElementById("code-editor-config").textContent);
            const { monaco } = await import(config.monacoUrl);
            return monaco.editor.getModels()[0].getLanguageId();
        });
        assert.equal(language, "python");
        await setCode("def merhaba():");
        await page.evaluate(async () => {
            const config = JSON.parse(document.getElementById("code-editor-config").textContent);
            const { monaco } = await import(config.monacoUrl);
            monaco.editor.getEditors()[0].focus();
        });
        await page.keyboard.press("Control+End");
        await page.keyboard.press("Enter");
        assert.ok((await code()).endsWith("\n    "), "Python otomatik girinti");
        assert.ok(await page.locator(".monaco-editor .line-numbers").count() > 0);
        await page.keyboard.press("Control+m");
        await page.keyboard.press("Tab");
        assert.equal(await page.evaluate(() => document.activeElement.closest(".monaco-editor") === null), true, "Editörden klavyeyle çıkılabilmeli");

        await setCode("print('taslak')");
        await page.selectOption("#editor-task", { label: "6 — Çıktı tahmini (Açıklama)" });
        assert.equal(await page.locator("#run-code").isDisabled(), true);
        assert.match(await page.locator("#editor-task-note").innerText(), /açıklama görevi/);
        await page.selectOption("#editor-task", "1");
        assert.equal(await code(), "print('taslak')");
        await setCode("print('Merhaba, dünya!')");
        const realRequests = [];
        page.on("request", request => { if (request.method() === "POST" && request.url().includes("?handler=")) realRequests.push(request); });
        const runResponse = page.waitForResponse(response => response.url().includes("handler=Calistir"));
        await page.click("#run-code");
        assert.equal(await page.locator("#run-code").isDisabled(), true);
        assert.equal(await page.locator("#test-code").isDisabled(), true);
        assert.equal(await page.locator("#editor-task").isDisabled(), true);
        await page.locator("#run-code").evaluate(button => button.click());
        assert.equal((await runResponse).status(), 200);
        await page.waitForFunction(() => document.getElementById("output-status").dataset.state === "success");
        assert.equal(realRequests.length, 1, "İşlem sürerken ikinci istek gönderilmemeli");
        assert.deepEqual(realRequests[0].postDataJSON(), { kod: "print('Merhaba, dünya!')" });
        assert.ok(realRequests[0].headers()["x-csrf-token"]);
        assert.equal(new URL(realRequests[0].url()).searchParams.get("no"), "1");
        const testResponse = page.waitForResponse(response => response.url().includes("handler=Test"));
        await page.click("#test-code");
        assert.equal((await testResponse).status(), 200);
        await page.waitForFunction(() => document.getElementById("output-status").textContent === "Test başarılı");

        let responseData;
        let responseStatus = 200;
        await page.route(/\/Egitimler\/Detay\/1\?handler=(Calistir|Test)/, route => route.fulfill({
            status: responseStatus, contentType: "application/json", body: JSON.stringify(responseData)
        }));
        const clickAndWait = async (button = "#test-code") => {
            await page.click(button);
            await page.waitForFunction(() => document.getElementById("output-status").dataset.state !== "loading");
        };
        responseData = { basarili: false, durum: "basarisiz", cikti: "<img src=x onerror=alert(1)>", hata: "<script>alert(1)</script>", cikisKodu: 0, ciktiEslesiyor: false, cikisKoduEslesiyor: true, kuralIhlalleri: ["<b>yasak</b>"] };
        await clickAndWait();
        assert.equal(await page.locator("#output-status").getAttribute("data-state"), "failure");
        assert.match(await page.locator("#code-stdout").innerText(), /<img/);
        assert.equal(await page.locator("#code-output img, #code-output script, #code-tests b").count(), 0);
        responseData = { basarili: true, durum: "tamamlandi", cikti: "", hata: "", gorevler: [] };
        await clickAndWait("#run-code");
        assert.equal(await page.locator("#code-stdout").innerText(), "(Standart çıktı boş.)");
        assert.equal(await page.locator("#stderr-block").isVisible(), false);
        assert.equal(await page.locator("#tests-block").isVisible(), false);
        responseData = { basarili: false, durum: "hata", cikti: "", hata: "SyntaxError: geçersiz kod" };
        await clickAndWait("#run-code");
        assert.equal(await page.locator("#output-status").getAttribute("data-state"), "error");
        for (const state of ["zaman_asimi", "cikti_siniri", "test_yok"]) {
            responseData = { basarili: false, durum: state, cikti: "", hata: "" };
            await clickAndWait();
            assert.notEqual(await page.locator("#output-status").getAttribute("data-state"), "success");
        }
        responseData = { basarili: true, durum: "tamamlandi", cikti: "pytest", hata: "", gorevler: [{ no: 1, durum: "basarili" }, { no: 2, durum: "atlanmis" }] };
        await clickAndWait();
        assert.equal(await page.locator("#output-status").innerText(), "Test başarılı");
        assert.match(await page.locator("#code-tests").innerText(), /Görev 1: Geçti/);
        for (const httpStatus of [401, 403, 409, 429, 500, 503]) {
            responseStatus = httpStatus; responseData = { mesaj: "sunucu" };
            await clickAndWait();
            assert.equal(await page.locator("#output-status").getAttribute("data-state"), "error");
            assert.equal(await page.locator("#run-code").isEnabled(), true);
            assert.equal(await page.locator("#output-login").isVisible(), httpStatus === 401);
            assert.equal(await page.locator("#stdout-block").isVisible(), false);
        }
        responseStatus = 200; responseData = { beklenmeyen: true };
        await clickAndWait();
        assert.equal(await page.locator("#output-status").innerText(), "Sonuç alınamadı");

        responseData = { basarili: true, durum: "basarili", cikti: "Merhaba, dünya!\n", hata: "", cikisKodu: 0, ciktiEslesiyor: true, cikisKoduEslesiyor: true, kuralIhlalleri: [] };
        await clickAndWait();

        for (const width of [1440, 768, 390, 320]) {
            await page.setViewportSize({ width, height: 1000 });
            await page.locator("#kod-editoru").scrollIntoViewIfNeeded();
            await page.waitForFunction(() => document.documentElement.scrollWidth <= innerWidth);
            if (process.env.EDITOR_SCREENSHOT_DIR && [1440, 390].includes(width))
                await page.screenshot({ path: path.join(process.env.EDITOR_SCREENSHOT_DIR, `monaco-${width}.png`) });
        }
        assert.deepEqual(errors, [], "Yakalanmamış JavaScript hatası olmamalı");
        assert.equal(consoleErrors.some(message => /worker|Could not create/i.test(message)), false, "Monaco worker yüklenmeli");
        // Ayrı sayfada Monaco engellenir; kalan arayüz ve textarea dürüstçe çalışır.
        const fallbackPage = await browser.newPage();
        await fallbackPage.route("**/lib/monaco/editor.js", route => route.abort());
        await fallbackPage.goto(`${base}/Egitimler/Detay/1`);
        await fallbackPage.waitForFunction(() => document.getElementById("editor-load-status").textContent.startsWith("Monaco yüklenemedi"));
        assert.equal(await fallbackPage.locator("#code-fallback").isVisible(), true);
        assert.equal(await fallbackPage.locator("#run-code").isEnabled(), true);
        console.log("PASS: real Monaco/Python/indent/keyboard exit; task drafts; exact POST+CSRF; duplicate-request lock; success/failure/empty/error/HTTP states; XSS-safe output; 4 responsive widths; local worker; honest fallback. Runner results are fixture/mocked, not real sandbox execution.");
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
