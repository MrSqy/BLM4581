import os

import calisma


def test_topla():
    assert calisma.topla(2, 3) == 5


def test_kasitli_hata_yalnizca_istenince_basarisiz_olur():
    beklenen = 99 if os.environ.get("KASITLI_HATA") == "1" else 5
    assert calisma.topla(2, 3) == beklenen
