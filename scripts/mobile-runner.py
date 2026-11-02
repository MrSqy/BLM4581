"""Yalnız Docker sandbox içinde çalışır; sonuçlar eğitim geri bildirimidir."""
import contextlib
import importlib
import io
import json
import re
import runpy
import sys
import traceback

class LimitedOutput(io.StringIO):
    def write(self, text):
        if self.tell() + len(text) > 24000:
            raise RuntimeError("Çıktı sınırı aşıldı.")
        return super().write(text)

out, err = LimitedOutput(), LimitedOutput()
tasks = {}
success = False
status = "hata"
try:
    with contextlib.redirect_stdout(out), contextlib.redirect_stderr(err):
        if sys.argv[1] == "calistir":
            runpy.run_path("calisma.py", run_name="__main__")
            success, status = True, "tamamlandi"
        else:
            import pytest

            class Results:
                @pytest.fixture
                def cozum(self):
                    return importlib.import_module("calisma")

                def pytest_runtest_logreport(self, report):
                    match = re.search(r"test_gorev_(\d+)(?:\D|$)", report.nodeid)
                    if not match:
                        return
                    n = match.group(1)
                    if report.failed:
                        tasks[n] = "basarisiz"
                    elif report.skipped and tasks.get(n) != "basarisiz":
                        tasks[n] = "atlanmis"
                    elif report.when == "call" and tasks.get(n) not in ("basarisiz", "atlanmis"):
                        tasks[n] = "basarili"

            code = pytest.main(["-q", "-p", "no:cacheprovider", "--tb=short", "test_alistirmalar.py"], plugins=[Results()])
            success = code == 0
            status = "tamamlandi" if success else ("test_yok" if code == 5 else "basarisiz")
except BaseException:
    # Kullanıcı kodundaki exit dahil süreç hataları başarı gibi sunulmaz.
    try:
        err.write(traceback.format_exc(limit=5))
    except RuntimeError:
        pass
print("MOBIL_SONUC=" + json.dumps(dict(basarili=success, durum=status,
    cikti=out.getvalue(), hata=err.getvalue(), gorevler=tasks), ensure_ascii=True))
