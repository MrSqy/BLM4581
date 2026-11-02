"""Yalnız aşağıdaki sabit, güvenilir fixture'ları yerelde çalıştırır; Docker izolasyon testi değildir."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

runner = Path(__file__).with_name("mobile-runner.py").resolve()
cases = [
    ("calistir", "print('merhaba')", "", "tamamlandi", {}),
    ("calistir", "raise ValueError('ornek')", "", "hata", {}),
    ("test", "deger = 2", "def test_gorev_1(cozum): assert cozum.deger == 2\ndef test_gorev_2(cozum): assert cozum.deger == 3", "basarisiz", {"1": "basarili", "2": "basarisiz"}),
    ("test", "pass", "import pytest\n@pytest.mark.skip\ndef test_gorev_1(): pass", "tamamlandi", {"1": "atlanmis"}),
    ("test", "pass", "", "test_yok", {}),
]
for mode, code, tests, status, tasks in cases:
    with tempfile.TemporaryDirectory() as tmp:
        Path(tmp, "calisma.py").write_text(code)
        Path(tmp, "test_alistirmalar.py").write_text(tests)
        result = subprocess.run([sys.executable, "-B", str(runner), mode], cwd=tmp,
            env={**os.environ, "PYTEST_DISABLE_PLUGIN_AUTOLOAD": "1", "PYTHONDONTWRITEBYTECODE": "1"},
            capture_output=True, text=True, timeout=15, check=True)
        data = json.loads(result.stdout.split("MOBIL_SONUC=")[-1])
        assert data["durum"] == status, data
        assert data["gorevler"] == tasks, data
print(f"{len(cases)} güvenilir runner fixture testi geçti (yerel Python; Docker değil).")
