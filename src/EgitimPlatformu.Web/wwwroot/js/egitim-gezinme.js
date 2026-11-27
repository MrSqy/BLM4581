// Yalnız paneli kaydır: uzun listede açık dersi bulurken sayfanın konumu değişmesin.
(() => {
    const body = document.getElementById("egitim-icerigi");
    const current = body?.querySelector('[aria-current="page"]');
    if (!body || !current) return;
    function revealCurrent() {
        const parent = body.getBoundingClientRect();
        const active = current.getBoundingClientRect();
        if (active.top < parent.top || active.bottom > parent.bottom)
            body.scrollTop += active.top - parent.top - 12;
    }
    body.addEventListener("shown.bs.collapse", revealCurrent);
    const desktop = window.matchMedia("(min-width: 992px)");
    desktop.addEventListener("change", revealCurrent);
    if (desktop.matches) revealCurrent();
})();
