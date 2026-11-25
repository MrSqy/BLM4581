#!/usr/bin/env python3
"""Eğitim zip'inden (dersler/<KISALTMA>-NN/) v2 içerik paketi üretir.

Bütün görevler `aciklama` türünde yüklenir: mevcut çalıştırıcı yalnız tek dosyalı
Python + stdout kontrolünü destekler; C# ve pytest tabanlı görevler orada
çalıştırılamaz. Başlangıç kodu görev açıklamasına kod bloğu olarak eklenir.
`cozumler/` hiç okunmaz. Zip açılmaz, yalnız okunur.

Örnek:
    python3 scripts/zip-paket.py ~/Masaüstü/BLM-CSharp-Egitim-3-ders.zip \
        --kisaltma CS --ad "C# dili" --aciklama "..." --sira 3 --dil csharp \
        docs/ornekler/cs-3-ders-v2.json
"""
import argparse
import json
import posixpath
import re
import sys
import zipfile

GOREV = re.compile(r"^#{2,3} Görev (\d+) — (.+?)\s*$")
DOSYA = re.compile(r"\*\*Dosya:\*\*\s*`(gorevler/[A-Za-z0-9_.-]+)`")
CALLOUT = re.compile(r"^(\s*>\s*)\[!(\w+)\][-+]?\s*(.*)$")
WIKI = re.compile(r"\[\[([^\]|#]+)(#[^\]|]*)?(?:\|([^\]]+))?\]\]")


def oku(z, yol):
    return z.read(yol).decode("utf-8").replace("\r\n", "\n")


def on_bilgiyi_at(metin):
    if metin.startswith("---\n"):
        son = metin.find("\n---\n", 4)
        if son != -1:
            return metin[son + 5:].lstrip("\n")
    return metin


def duz_markdown(metin):
    satirlar, cit = [], None
    for satir in metin.split("\n"):
        sade = satir.lstrip()
        if sade.startswith(("```", "~~~")):
            isaret = sade[:3]
            cit = None if cit == isaret else (cit or isaret)
        elif cit is None:
            m = CALLOUT.match(satir)
            if m:
                baslik = m.group(3).strip()
                satir = f"{m.group(1)}**{m.group(2)}{': ' + baslik if baslik else ''}**"
            satir = WIKI.sub(lambda w: w.group(3) or w.group(1), satir)
        satirlar.append(satir)
    return "\n".join(satirlar)


def bolumle(alistirmalar):
    """Giriş metni ve (no, başlık, gövde) görev listesi döndürür; kod çitleri içi atlanır."""
    giris, gorevler, simdiki, cit = [], [], None, False
    for satir in alistirmalar.split("\n"):
        if satir.lstrip().startswith(("```", "~~~")):
            cit = not cit
        m = None if cit else GOREV.match(satir)
        if m:
            simdiki = [int(m.group(1)), m.group(2).strip(), []]
            gorevler.append(simdiki)
        elif not cit and simdiki is not None and re.match(r"^#{1,2} ", satir) and not satir.startswith("### "):
            simdiki = None
        elif simdiki is not None:
            simdiki[2].append(satir)
        else:
            giris.append(satir)
    return "\n".join(giris).strip(), [(no, b, "\n".join(g).strip()) for no, b, g in gorevler]


def ders_baslik(kod, readme):
    for satir in readme.split("\n"):
        if satir.startswith("# "):
            baslik = satir[2:].strip()
            return re.sub(rf"^({re.escape(kod)}|\d+)\s*[—-]\s*", "", baslik)[:150]
    return kod


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("zip")
    ap.add_argument("cikti")
    ap.add_argument("--kisaltma", required=True)
    ap.add_argument("--ad", required=True)
    ap.add_argument("--aciklama", default="")
    ap.add_argument("--sira", type=int, required=True)
    ap.add_argument("--dil", required=True, help="Başlangıç kodu blok dili (python, csharp...)")
    a = ap.parse_args()

    with zipfile.ZipFile(a.zip) as z:
        adlar = set(z.namelist())
        desen = re.compile(rf"^([^/]+/)?dersler/({re.escape(a.kisaltma)}-\d+)/README\.md$")
        dersler = sorted({(m.group(1) or "", m.group(2)) for ad in adlar if (m := desen.match(ad))},
                         key=lambda d: int(d[1].rsplit("-", 1)[1]))
        if not dersler:
            sys.exit(f"{a.zip} içinde dersler/{a.kisaltma}-NN/README.md bulunamadı.")
        egitimler = []
        for sira, (kok, kod) in enumerate(dersler, 1):
            klasor = f"{kok}dersler/{kod}/"
            readme = duz_markdown(on_bilgiyi_at(oku(z, klasor + "README.md")))
            giris, gorevler = bolumle(duz_markdown(on_bilgiyi_at(oku(z, klasor + "ALISTIRMALAR.md"))))
            if not gorevler:
                sys.exit(f"{kod}: ALISTIRMALAR.md içinde görev başlığı yok.")
            paket_gorevleri = []
            for no, baslik, govde in gorevler:
                m = DOSYA.search(govde)
                if m:
                    yol = posixpath.normpath(klasor + m.group(1))
                    if not yol.startswith(klasor + "gorevler/") or yol not in adlar:
                        print(f"uyarı: {kod} görev {no}: {m.group(1)} bulunamadı", file=sys.stderr)
                    else:
                        kod_metni = oku(z, yol).rstrip("\n")
                        govde += (f"\n\n**Başlangıç kodu** (`{m.group(1)}`):\n\n```{a.dil}\n{kod_metni}\n```"
                                  "\n\n> Bu görevin kodu uygulamada çalıştırılmaz; kendi ortamında çalıştır.")
                paket_gorevleri.append({"no": no, "baslik": baslik[:200], "aciklama": govde,
                                        "tur": "aciklama", "baslangicDosyasi": None,
                                        "baslangicKodu": "", "kontrol": None})
            icerik = readme + ("\n\n---\n\n## Alıştırmalar\n\n" + giris if giris else "")
            egitimler.append({"kod": kod, "baslik": ders_baslik(kod, readme), "sira": sira,
                              "icerikMarkdown": icerik, "baslangicKodu": "", "testKodu": "",
                              "gorevler": paket_gorevleri})

    paket = {"surum": 2, "kategoriler": [{"kisaltma": a.kisaltma, "ad": a.ad, "aciklama": a.aciklama,
                                          "sira": a.sira, "egitimler": egitimler}]}
    with open(a.cikti, "w", encoding="utf-8") as f:
        json.dump(paket, f, ensure_ascii=False, indent=2)
        f.write("\n")
    print(f"{a.cikti}: {len(egitimler)} ders, {sum(len(e['gorevler']) for e in egitimler)} görev.")


if __name__ == "__main__":
    main()
