const configElement = document.getElementById("code-editor-config");

if (configElement) {
    const config = JSON.parse(configElement.textContent);
    const find = id => document.getElementById(id);
    const taskSelect = find("editor-task");
    const fallback = find("code-fallback");
    const runButton = find("run-code");
    const testButton = find("test-code");
    const output = find("code-output");
    const models = new Map();
    const drafts = new Map();
    let editor;
    let monaco;
    let busy = false;
    let currentTask = config.gorevBazli ? config.gorevler.find(task => task.tur === "kod") ?? config.gorevler[0] : null;
    const key = () => currentTask?.id ?? "ders";
    const initialCode = () => currentTask?.baslangicKodu ?? config.baslangicKodu ?? "";
    const executable = () => !config.gorevBazli || (currentTask?.tur === "kod" && currentTask.kontrolVar && Number.isInteger(currentTask.no));
    const getCode = () => editor ? editor.getValue() : fallback.value;

    function updateControls() {
        const canRun = config.izinli && executable() && !busy;
        runButton.disabled = !canRun;
        testButton.disabled = !canRun || (!config.gorevBazli && !config.testVar);
        if (taskSelect) taskSelect.disabled = busy;
        find("code-loading").hidden = !busy;
        output.setAttribute("aria-busy", String(busy));
        fallback.readOnly = busy || !executable();
        editor?.updateOptions({ readOnly: busy || !executable() });
    }

    function status(label, state, message) {
        find("output-status").textContent = label;
        find("output-status").dataset.state = state;
        find("output-message").textContent = message;
    }

    function clearOutput() {
        ["stdout", "stderr"].forEach(name => {
            find(`${name}-block`).hidden = true;
            find(`code-${name}`).textContent = "";
        });
        find("code-tests").replaceChildren();
        find("tests-block").hidden = true;
        find("output-login").hidden = true;
        find("result-cards").replaceChildren();
        find("result-cards-section").hidden = true;
        find("result-cards-empty").hidden = false;
    }

    function showCode() {
        const value = drafts.get(key()) ?? initialCode();
        fallback.value = value;
        if (editor) {
            if (!models.has(key())) models.set(key(), monaco.editor.createModel(value, "python"));
            editor.setModel(models.get(key()));
        }
        if (taskSelect && currentTask) {
            taskSelect.value = String(currentTask.id);
            find("editor-task-link").href = `#gorev-${currentTask.id}`;
        }
        const note = currentTask?.tur === "aciklama"
            ? "Bu görev bir açıklama görevi. Yanıtını görevdeki ölçütlere göre hazırla; otomatik çalıştırma ve test yok."
            : !executable() ? "Bu görev için otomatik kontrol tanımlanmamış. Görev açıklamasını incele."
            : !config.gorevBazli && !config.testVar ? "Başlangıç kodunu düzenleyip çalıştırabilirsin. Bu ders için otomatik test henüz tanımlanmamış."
            : "Çalıştır ve Test Et aynı kodu kullanır. Görev değiştirirken bu sayfadaki taslağın korunur.";
        find("editor-task-note").textContent = note;
        updateControls();
    }

    taskSelect?.addEventListener("change", () => {
        if (busy) return;
        drafts.set(key(), getCode());
        currentTask = config.gorevler.find(task => String(task.id) === taskSelect.value);
        clearOutput();
        status("Henüz çalıştırılmadı", "idle", "Seçili görevin sonuçları burada görünecek.");
        showCode();
    });

    function addTestResult(text) {
        const item = document.createElement("li");
        item.textContent = text;
        find("code-tests").append(item);
        find("tests-block").hidden = false;
    }

    function renderResult(data, test) {
        // Yalnız mevcut runner sözleşmesi kabul edilir. HTML hiçbir yerde yorumlanmaz.
        const states = new Set(["basarili", "basarisiz", "tamamlandi", "hata", "zaman_asimi", "cikti_siniri", "test_yok"]);
        if (!data || typeof data.basarili !== "boolean" || !states.has(data.durum)
            || typeof data.cikti !== "string" || typeof data.hata !== "string")
            throw new Error("invalid-response");

        const labels = {
            basarili: ["Test başarılı", "success", "Kontrol ölçütleri sağlandı."],
            basarisiz: ["Test başarısız", "failure", "Çözüm henüz tüm kontrol ölçütlerini karşılamıyor."],
            tamamlandi: test ? ["Test başarılı", "success", "Kontrol ölçütleri sağlandı."] : ["Çalıştırma tamamlandı", "success", "Program tamamlandı."],
            test_yok: ["Test bulunamadı", "failure", "Bu çalıştırmada otomatik test bulunamadı; çözüm başarılı sayılmadı."],
            hata: ["Çalıştırma hatası", "error", "Program çalışırken bir hata oluştu. Hata çıktısını incele."],
            zaman_asimi: ["Süre sınırı aşıldı", "error", "Program zamanında tamamlanmadı. Döngülerini kontrol et."],
            cikti_siniri: ["Çıktı sınırı aşıldı", "error", "Program çok fazla çıktı üretti. Çıktı kısaltılmış olabilir."]
        };
        const [label, state, message] = labels[data.durum];
        status(label, state, message);
        find("stdout-block").hidden = false;
        find("code-stdout").textContent = data.cikti.length ? data.cikti : "(Standart çıktı boş.)";
        if (data.hata.length) {
            find("stderr-block").hidden = false;
            find("code-stderr").textContent = data.hata;
        }
        if (Number.isInteger(data.cikisKodu)) addTestResult(`Program çıkış kodu: ${data.cikisKodu}`);
        if (!test) return;
        renderCards(data);
        if (typeof data.ciktiEslesiyor === "boolean") addTestResult(`Beklenen çıktı: ${data.ciktiEslesiyor ? "eşleşiyor" : "eşleşmiyor"}`);
        if (typeof data.cikisKoduEslesiyor === "boolean") addTestResult(`Beklenen çıkış kodu: ${data.cikisKoduEslesiyor ? "eşleşiyor" : "eşleşmiyor"}`);
        if (Array.isArray(data.kuralIhlalleri)) data.kuralIhlalleri.forEach(rule => addTestResult(`Kural ihlali: ${rule}`));
        const taskLabels = { basarili: "Geçti", basarisiz: "Kaldı", atlanmis: "Atlandı", test_yok: "Otomatik test yok" };
        if (Array.isArray(data.gorevler)) data.gorevler.forEach(task => {
            if (Number.isInteger(task.no)) addTestResult(`Görev ${task.no}: ${taskLabels[task.durum] ?? "Sonuç alınamadı"}`);
        });
    }

    function renderCards(data) {
        find("result-cards-section").hidden = false;
        const labels = { basarili: ["Başarılı", "success"], basarisiz: ["Başarısız", "failure"], atlanmis: ["Atlandı", "neutral"], test_yok: ["Test bulunamadı", "neutral"], hata: ["Hata", "error"], zaman_asimi: ["Süre aşıldı", "error"], cikti_siniri: ["Çıktı sınırı aşıldı", "error"] };
        const add = (name, state, detail) => {
            const card = document.createElement("article");
            card.className = "test-result-card";
            card.setAttribute("role", "listitem");
            const [label, color] = labels[state] ?? ["Sonuç bilinmiyor", "neutral"];
            card.dataset.state = color;
            const title = document.createElement("h5"); title.textContent = name;
            const badge = document.createElement("span"); badge.className = "result-badge"; badge.textContent = label;
            card.append(title, badge);
            if (detail) { const text = document.createElement("pre"); text.textContent = detail; card.append(text); }
            find("result-cards").append(card);
            find("result-cards-empty").hidden = true;
        };
        if (Array.isArray(data.gorevler)) data.gorevler.forEach(task => {
            if (!Number.isInteger(task.no)) return;
            const taskInfo = config.gorevler.find(g => g.no === task.no);
            add(`Görev ${task.no}${taskInfo ? ` — ${taskInfo.baslik}` : ""}`, task.durum,
                typeof task.hata === "string" ? task.hata : task.durum === "basarisiz" ? "Ayrıntılı hata bu görev için ayrı verilmedi. Çıktı panelini incele." : "");
        });
        if (config.gorevBazli && currentTask?.tur === "kod") {
            add(`Görev ${currentTask.no} — ${currentTask.baslik}`, data.durum === "tamamlandi" ? (data.basarili ? "basarili" : "hata") : data.durum, data.hata);
            if (typeof data.ciktiEslesiyor === "boolean") add("Standart çıktı kontrolü", data.ciktiEslesiyor ? "basarili" : "basarisiz", "");
            if (typeof data.cikisKoduEslesiyor === "boolean") add("Program çıkış kodu kontrolü", data.cikisKoduEslesiyor ? "basarili" : "basarisiz", "");
        }
    }

    async function sendCode(test) {
        if (busy || !config.izinli || !executable()) return;
        clearOutput();
        const kod = getCode();
        if (!kod.trim() || kod.length > 32000) {
            status("Kodunu kontrol et", "error", "1–32000 karakter arasında çalıştırılacak kod yaz.");
            return;
        }
        busy = true;
        updateControls();
        status(test ? "Test ediliyor…" : "Çalıştırılıyor…", "loading", "Sonuç bekleniyor.");
        const controller = new AbortController();
        const timeout = window.setTimeout(() => controller.abort(), config.istekZamanAsimiMs ?? 60000);
        try {
            const url = new URL(test ? config.testUrl : config.calistirUrl, location.href);
            if (config.gorevBazli) url.searchParams.set("no", String(currentTask.no));
            const response = await fetch(url, {
                method: "POST",
                credentials: "same-origin",
                redirect: "error",
                headers: { "Content-Type": "application/json", "Accept": "application/json", "X-CSRF-TOKEN": find("code-actions").querySelector('[name="__RequestVerificationToken"]').value },
                body: JSON.stringify({ kod }),
                signal: controller.signal
            });
            if (!response.ok) {
                const messages = {
                    400: "Kod veya güvenlik doğrulaması geçersiz. Kodunu kontrol et; gerekirse sayfayı yenilemeden önce kodunu kopyala.",
                    401: "Oturumun sona ermiş olabilir. Bu işlem için oturum aç.",
                    403: "Bu işlem için erişim iznin yok.",
                    404: "Ders veya aktif görev bulunamadı. Kodunu kopyalayıp sayfayı yenileyebilirsin.",
                    409: "Seçili görevde otomatik kontrol yok veya içerik değişmiş. Kodunu kopyalayıp sayfayı yenileyebilirsin.",
                    413: "Gönderilen kod çok büyük. En fazla 32000 karakter kullan.",
                    429: "Çok fazla istek gönderildi. Bir süre bekleyip yeniden dene.",
                    503: "Kod çalıştırma servisi şu anda kullanılamıyor veya meşgul. Daha sonra yeniden dene."
                };
                let message = messages[response.status] ?? "Sunucuda bir sorun oluştu. Kodun editörde duruyor; daha sonra yeniden deneyebilirsin.";
                if (response.status === 503) {
                    const error = await response.json().catch(() => null);
                    if (error?.durum === "gonderim_kayit_hatasi") {
                        message = "Test tamamlandı ancak gönderim kaydedilemedi. Kodun editörde duruyor.";
                    } else if (error?.durum === "kuyruk_dolu") {
                        const seconds = Number(response.headers.get("Retry-After"));
                        message = Number.isInteger(seconds) && seconds > 0
                            ? `Sandbox yoğun. ${seconds} saniye sonra yeniden dene.`
                            : "Sandbox yoğun. Birkaç saniye sonra yeniden dene.";
                    } else if (error?.durum === "altyapi_yok") {
                        message = "Kod çalıştırma altyapısına şu anda ulaşılamıyor.";
                    }
                }
                status("İşlem tamamlanamadı", "error", message);
                find("output-login").hidden = response.status !== 401;
                return;
            }
            renderResult(await response.json(), test);
        } catch (error) {
            status("Sonuç alınamadı", "error", error.name === "AbortError"
                ? "Sunucudan zamanında yanıt alınamadı. Bir süre bekleyip yeniden dene."
                : "Bağlantı kurulamadı veya sunucudan beklenen yanıt alınamadı. Kodun editörde duruyor.");
        } finally {
            window.clearTimeout(timeout);
            busy = false;
            updateControls();
        }
    }

    runButton.addEventListener("click", () => sendCode(false));
    testButton.addEventListener("click", () => sendCode(true));
    find("code-actions").addEventListener("submit", event => event.preventDefault());
    showCode();

    // Yerel ESM paketi; CDN veya tarayıcıda Python çalıştıran bir motor kullanılmaz.
    try {
        ({ monaco } = await import(config.monacoUrl));
        const host = find("monaco-host");
        models.set(key(), monaco.editor.createModel(fallback.value, "python"));
        host.hidden = false;
        editor = monaco.editor.create(host, {
            model: models.get(key()),
            theme: "vs",
            automaticLayout: true,
            lineNumbers: "on",
            autoIndent: "full",
            tabSize: 4,
            insertSpaces: true,
            minimap: { enabled: false },
            fontSize: 14,
            lineHeight: 23,
            wordWrap: "on",
            scrollBeyondLastLine: false,
            padding: { top: 16, bottom: 16 },
            ariaLabel: "Python kod editörü",
            accessibilitySupport: "auto"
        });
        find("editor-fallback").hidden = true;
        find("editor-load-status").textContent = "Monaco editörü hazır.";
        find("editor-load-status").classList.add("visually-hidden");
        updateControls();
    } catch {
        find("monaco-host").hidden = true;
        find("editor-fallback").hidden = false;
        find("editor-load-status").textContent = "Monaco yüklenemedi. Kodunu düz metin alanında düzenleyebilir, servis erişilebiliyorsa aynı düğmelerle gönderebilirsin.";
        updateControls();
    }

    window.addEventListener("pagehide", event => {
        if (event.persisted) return;
        editor?.dispose();
        models.forEach(model => model.dispose());
    });
}
