"""Konteyner içindeki Jupyter oturumunu tek notebook çalışması için yönetir."""

import asyncio
import json
import os
import secrets
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path
from urllib.error import URLError
from urllib.request import Request, urlopen
from uuid import uuid4

from tornado.httpclient import HTTPRequest
from tornado.websocket import websocket_connect

ADRES = "127.0.0.1:8888"
CIKTI_SINIRI = 48 * 1024
CIKTI_TURLERI = {
    "stream": ("name", "text"),
    "display_data": ("data", "metadata"),
    "execute_result": ("data", "metadata", "execution_count"),
    "error": ("ename", "evalue", "traceback"),
}


def api(token, yol, yontem="GET", veri=None):
    istek = Request(
        f"http://{ADRES}/api/{yol}",
        data=None if veri is None else json.dumps(veri).encode(),
        headers={"Authorization": f"token {token}", "Content-Type": "application/json"},
        method=yontem,
    )
    with urlopen(istek, timeout=3) as yanit:
        return json.load(yanit) if yanit.status != 204 else None


def sunucuyu_bekle(sunucu, token):
    son = time.monotonic() + 8
    while sunucu.poll() is None and time.monotonic() < son:
        try:
            api(token, "kernels")
            return
        except URLError:
            time.sleep(0.1)
    raise RuntimeError("Jupyter Server hazır olmadı.")


async def hucre_calistir(kanal, oturum, kod, kalan):
    kimlik = uuid4().hex
    await kanal.write_message({
        "header": {
            "msg_id": kimlik, "session": oturum, "username": "notebook",
            "date": datetime.now(timezone.utc).isoformat(), "version": "5.3",
            "msg_type": "execute_request",
        },
        "parent_header": {}, "metadata": {}, "channel": "shell",
        "content": {
            "code": kod, "silent": False, "store_history": True,
            "user_expressions": {}, "allow_stdin": False, "stop_on_error": True,
        },
    })

    durum, bitti, ciktilar = None, False, []
    while durum is None or not bitti:
        ham = await kanal.read_message()
        if ham is None:
            raise RuntimeError("Jupyter bağlantısı kapandı.")
        mesaj = json.loads(ham)
        if mesaj.get("parent_header", {}).get("msg_id") != kimlik:
            continue
        tur = mesaj["header"]["msg_type"]
        icerik = mesaj["content"]
        if mesaj["channel"] == "shell" and tur == "execute_reply":
            durum = icerik["status"]
        if mesaj["channel"] != "iopub":
            continue
        if tur == "status" and icerik["execution_state"] == "idle":
            bitti = True
        if tur in CIKTI_TURLERI:
            cikti = {"output_type": tur, **{alan: icerik[alan] for alan in CIKTI_TURLERI[tur]}}
            kalan -= len(json.dumps(cikti)) + 2
            if kalan < 0:
                return {"durum": "cikti_siniri", "ciktilar": ciktilar}, kalan
            ciktilar.append(cikti)
    return {"durum": durum, "ciktilar": ciktilar}, kalan


async def calistir(token, cekirdek, hucreler):
    oturum = uuid4().hex
    kanal = await websocket_connect(HTTPRequest(
        f"ws://{ADRES}/api/kernels/{cekirdek}/channels?session_id={oturum}",
        headers={"Authorization": f"token {token}"},
        connect_timeout=4,
    ), max_message_size=1024 * 1024)
    sonuclar = []
    kalan = CIKTI_SINIRI
    try:
        for kod in hucreler:
            sonuc, kalan = await hucre_calistir(kanal, oturum, kod, kalan)
            sonuclar.append(sonuc)
            if kalan < 0:
                return {"durum": "cikti_siniri", "hucreler": sonuclar}
    finally:
        kanal.close()
    return {"durum": "tamamlandi", "hucreler": sonuclar}


def ana():
    hucreler = json.loads(Path("/istek.json").read_text(encoding="utf-8"))
    token = secrets.token_urlsafe(32)
    with subprocess.Popen(
        [sys.executable, "-m", "jupyter_server", "--no-browser", "--ip=127.0.0.1",
         "--port=8888", "--ServerApp.port_retries=0", "--ServerApp.root_dir=/tmp"],
        env={**os.environ, "JUPYTER_TOKEN": token},
        stdout=sys.stderr,
        stderr=subprocess.DEVNULL,
    ) as sunucu:
        cekirdek = None
        try:
            sunucuyu_bekle(sunucu, token)
            cekirdek = api(token, "kernels", "POST", {"name": "python3"})["id"]
            sonuc = asyncio.run(calistir(token, cekirdek, hucreler))
        finally:
            try:
                if cekirdek is not None:
                    api(token, f"kernels/{cekirdek}", "DELETE")
            finally:
                sunucu.terminate()
                try:
                    sunucu.wait(timeout=2)
                except subprocess.TimeoutExpired:
                    sunucu.kill()
        print(json.dumps(sonuc))


if __name__ == "__main__":
    ana()
