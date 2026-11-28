// Notebook sayfası: işlemden sonra ilgili hücreye odaklanır, metin alanını içeriğe göre büyütür.
(() => {
    const form = document.getElementById("notebook-formu");
    if (!form) return;

    const buyut = (alan) => {
        alan.style.height = "auto";
        alan.style.height = `${alan.scrollHeight + 2}px`;
    };
    form.querySelectorAll(".cell-source").forEach((alan) => {
        buyut(alan);
        alan.addEventListener("input", () => buyut(alan));
    });

    const odak = form.dataset.odak;
    if (odak !== undefined && odak !== "") {
        const hucre = document.getElementById(`hucre-${odak}`);
        hucre?.scrollIntoView({ block: "center" });
        hucre?.querySelector(".cell-source")?.focus({ preventScroll: true });
    }
})();
