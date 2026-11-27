// Ek bağımlılık gerektirmez: node --test scripts/test-egitim-gezinme.mjs
// DOM taklitleriyle gerçek istemci dosyaları çalıştırılır; görsel tarayıcı testi değildir.
import { readFile } from "node:fs/promises";
import vm from "node:vm";
import test from "node:test";
import assert from "node:assert/strict";

const editorSource = await readFile(new URL("../src/EgitimPlatformu.Web/wwwroot/js/kod-editoru.js", import.meta.url), "utf8");
const navigationSource = await readFile(new URL("../src/EgitimPlatformu.Web/wwwroot/js/egitim-gezinme.js", import.meta.url), "utf8");

async function editorPage() {
    const elements = new Map();
    const listeners = new Map();
    const element = id => {
        if (!elements.has(id)) elements.set(id, {
            value: "", textContent: "", dataset: {}, handlers: {}, classList: { add() {} },
            addEventListener(name, callback) { this.handlers[name] = callback; },
            setAttribute() {}, replaceChildren() {}, append() {}
        });
        return elements.get(id);
    };
    element("code-editor-config").textContent = JSON.stringify({
        izinli: true, gorevBazli: true, monacoUrl: "./monaco-testte-yok.js",
        gorevler: [1, 2].map(id => ({ id, no: id, tur: "kod", kontrolVar: true, baslangicKodu: `# Görev ${id}` }))
    });
    await vm.runInNewContext(`(async () => { ${editorSource}\n })()`, {
        document: { getElementById: element },
        window: { addEventListener: (name, callback) => listeners.set(name, callback), removeEventListener: name => listeners.delete(name) }
    });
    return {
        listeners,
        edit(value) { element("code-fallback").value = value; element("code-fallback").handlers.input(); },
        select(id) { element("editor-task").value = String(id); element("editor-task").handlers.change(); },
        code: () => element("code-fallback").value,
        warning: () => listeners.has("beforeunload")
    };
}

test("Değişmeyen kod uyarmaz; düzenleme uyarır, geri alma uyarıyı kaldırır", async () => {
    const page = await editorPage();
    assert.equal(page.warning(), false);
    page.edit("print('taslak')");
    assert.equal(page.warning(), true);
    let prevented = false;
    const event = { preventDefault() { prevented = true; } };
    page.listeners.get("beforeunload")(event);
    assert.equal(prevented, true); assert.equal(event.returnValue, "");
    page.edit("# Görev 1");
    assert.equal(page.warning(), false);
});

test("Başka görevde kalan taslak uyarıyı korur; bütün değişiklikler geri alınınca kaldırır", async () => {
    const page = await editorPage();
    page.edit("birinci taslak"); page.select(2);
    assert.equal(page.code(), "# Görev 2"); assert.equal(page.warning(), true);
    page.edit("ikinci taslak"); page.select(1);
    assert.equal(page.code(), "birinci taslak");
    page.edit("# Görev 1"); assert.equal(page.warning(), true);
    page.select(2); page.edit("# Görev 2");
    assert.equal(page.warning(), false);
});

function menu(desktop, activeTop = 500) {
    const callbacks = {};
    const body = {
        scrollTop: 0,
        querySelector: () => ({ getBoundingClientRect: () => ({ top: activeTop, bottom: activeTop + 50 }) }),
        getBoundingClientRect: () => ({ top: 100, bottom: 400 }),
        addEventListener(name, callback) { callbacks[name] = callback; }
    };
    vm.runInNewContext(navigationSource, {
        document: { getElementById: () => body },
        window: { matchMedia: () => ({ matches: desktop, addEventListener(name, callback) { callbacks[name] = callback; } }) }
    });
    return { body, callbacks };
}

test("Uzun masaüstü menüsü açık dersi yalnız panelin içinde görünür yapar", () => {
    assert.equal(menu(true).body.scrollTop, 388);
    assert.equal(menu(true, 200).body.scrollTop, 0);
});

test("Mobil menü açıldığında açık ders görünür yapılır", () => {
    const page = menu(false);
    assert.equal(page.body.scrollTop, 0);
    page.callbacks["shown.bs.collapse"]();
    assert.equal(page.body.scrollTop, 388);
});
