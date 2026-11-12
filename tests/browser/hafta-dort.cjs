const { chromium } = require(process.env.PLAYWRIGHT_MODULE_PATH || "playwright");
const assert = require("node:assert/strict");
const path = require("node:path");

(async () => {
    const base = process.env.WEEK4_TEST_URL;
    assert.ok(base, "Yalnız izole SQLite fixture adresi WEEK4_TEST_URL olarak verilmeli.");
    const browser = await chromium.launch({ headless: true, ...(process.env.EDITOR_BROWSER_PATH ? { executablePath: process.env.EDITOR_BROWSER_PATH } : {}) });
    try {
        const page = await browser.newPage({ viewport: { width: 1440, height: 1000 } });
        const errors = []; page.on("pageerror", e => errors.push(e.message));
        await page.goto(`${base}/Yonetim/Kategoriler`);
        assert.match(page.url(), /Hesap\/Giris/);
        await page.getByLabel("E-posta adresi", { exact: true }).fill("admin@example.test");
        await page.getByLabel("Şifre", { exact: true }).fill("Guvenli123!");
        await Promise.all([page.waitForNavigation(), page.getByRole("button", { name: "Oturum aç", exact: true }).click()]);
        await page.goto(`${base}/Yonetim/Kategoriler`);
        await page.getByLabel("Kategori adı", { exact: true }).fill("Tarayıcı kategorisi");
        await page.getByLabel("Kısaltma", { exact: true }).fill("TAR");
        await Promise.all([page.waitForNavigation(), page.getByRole("button", { name: "Kaydet", exact: true }).click()]);
        let row = page.getByRole("row").filter({ hasText: "Tarayıcı kategorisi" });
        assert.equal(await row.count(), 1);
        await row.getByRole("link", { name: "Düzenle", exact: true }).click();
        await page.getByLabel("Kategori adı", { exact: true }).fill("Düzenlenen kategori");
        await Promise.all([page.waitForNavigation(), page.getByRole("button", { name: "Kaydet", exact: true }).click()]);
        row = page.getByRole("row").filter({ hasText: "Düzenlenen kategori" });
        await row.getByRole("link", { name: "Sil", exact: true }).click();
        assert.equal(await page.locator(".delete-confirmation").isVisible(), true);
        await page.getByLabel("Silme işlemini onaylıyorum.").check();
        await Promise.all([page.waitForNavigation(), page.getByRole("button", { name: "Kaydı sil", exact: true }).click()]);
        assert.equal(await page.getByRole("row").filter({ hasText: "Düzenlenen kategori" }).count(), 0);
        for (const width of [1440, 768, 390, 320]) {
            await page.setViewportSize({ width, height: 1000 });
            for (const route of ["/Yonetim", "/Yonetim/Kategoriler", "/Yonetim/Egitimler", "/Yonetim/Gorevler", "/Gonderimler"]) {
                await page.goto(base + route);
                const sw = await page.evaluate(() => document.documentElement.scrollWidth);
                assert.ok(sw <= width, `${route} ${width}px taşma: ${sw}`);
                if (process.env.EDITOR_SCREENSHOT_DIR && width === 1440 && route === "/Yonetim")
                    await page.screenshot({ path: path.join(process.env.EDITOR_SCREENSHOT_DIR, "hafta4-yonetim.png") });
            }
        }
        assert.match(await page.locator("main").innerText(), /Henüz gönderimin yok/);
        await page.setViewportSize({ width: 1440, height: 1000 });
        await page.goto(`${base}/Egitimler/Detay/1`);
        await page.waitForFunction(() => document.getElementById("editor-load-status").textContent === "Monaco editörü hazır.");
        let result;
        let status = 200;
        let headers = {};
        await page.route(/\/Egitimler\/Detay\/\d+\?handler=Test/, route =>
            route.fulfill({ status, headers, contentType: "application/json", body: JSON.stringify(result) }));
        const test = async () => {
            await page.click("#test-code");
            await page.waitForFunction(() => document.getElementById("output-status").dataset.state !== "loading");
        };
        result = { basarili: true, durum: "basarili", cikti: "selam", hata: "", ciktiEslesiyor: true, cikisKoduEslesiyor: true };
        await test(); assert.equal(await page.locator(".test-result-card").count(), 3);
        assert.equal(await page.locator('.test-result-card[data-state="success"]').count(), 3);
        result = { basarili: false, durum: "basarisiz", cikti: "x", hata: "<img src=x onerror=alert(1)>", ciktiEslesiyor: false, cikisKoduEslesiyor: true };
        await test(); assert.equal(await page.locator(".test-result-card img").count(), 0);
        assert.equal(await page.locator('.test-result-card[data-state="failure"]').count(), 2);
        assert.match(await page.locator(".test-result-card pre").innerText(), /<img/);
        result = { basarili: false, durum: "zaman_asimi", cikti: "", hata: "Süre aşıldı" };
        await test(); assert.equal(await page.locator('.test-result-card[data-state="error"]').count(), 1);
        status = 503;
        result = { durum: "gonderim_kayit_hatasi" };
        await test();
        assert.match(await page.locator("#output-message").innerText(), /gönderim kaydedilemedi/);
        result = { durum: "kuyruk_dolu" };
        headers = { "Retry-After": "5" };
        await test();
        assert.match(await page.locator("#output-message").innerText(), /5 saniye sonra/);
        result = { durum: "altyapi_yok" };
        await test();
        assert.match(await page.locator("#output-message").innerText(), /altyapısına şu anda ulaşılamıyor/);
        status = 200;
        headers = {};
        await page.goto(`${base}/Egitimler/Detay/2`);
        await page.waitForFunction(() => document.getElementById("editor-load-status").textContent === "Monaco editörü hazır.");
        result = { basarili: true, durum: "tamamlandi", cikti: "", hata: "", gorevler: [{ no: 1, durum: "basarili" }, { no: 2, durum: "basarisiz" }] };
        await test(); assert.equal(await page.locator(".test-result-card").count(), 2);
        result = { basarili: false, durum: "test_yok", cikti: "", hata: "", gorevler: [] };
        await test(); assert.equal(await page.locator("#result-cards-empty").isVisible(), true);
        assert.equal(await page.locator(".test-result-card").count(), 0);
        assert.deepEqual(errors, []);
        console.log("PASS: real Identity admin login; category add/edit/delete confirmation; responsive admin/history at 4 widths; empty history; result cards success/failure/timeout/empty, distinct 503 errors and safe text. History persistence and runner are not real backend acceptance.");
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
