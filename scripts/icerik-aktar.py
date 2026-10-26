#!/usr/bin/env python3
"""Yönetici oturumu ve antiforgery ile yerel Web API'ye içerik aktarır."""
import argparse
import getpass
import http.cookiejar
import json
import sys
from html.parser import HTMLParser
from urllib.error import HTTPError, URLError
from urllib.parse import urlencode, urlparse
from urllib.request import HTTPCookieProcessor, HTTPRedirectHandler, Request, build_opener


class TokenParser(HTMLParser):
    token = None

    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "input" and attrs.get("name") == "__RequestVerificationToken":
            self.token = attrs.get("value")


class YerelYonlendirme(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        if urlparse(req.full_url).netloc != urlparse(newurl).netloc:
            raise ValueError("Başka sunucuya yönlendirme reddedildi.")
        return super().redirect_request(req, fp, code, msg, headers, newurl)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("dosya")
    parser.add_argument("--url", default="http://localhost:5080")
    parser.add_argument("--eposta", required=True)
    args = parser.parse_args()
    base = args.url.rstrip("/")
    url = urlparse(base)
    if url.scheme not in ("http", "https") or not url.hostname or url.username or url.password:
        raise ValueError("Geçerli bir HTTP(S) adresi verin.")
    if url.scheme == "http" and url.hostname not in ("localhost", "127.0.0.1", "::1"):
        raise ValueError("Yerel adres dışındaki sunucularda HTTPS kullanın.")
    with open(args.dosya, encoding="utf-8") as source:
        payload = json.dumps(json.load(source), ensure_ascii=False).encode("utf-8")
    if len(payload) > 5 * 1024 * 1024:
        raise ValueError("Paket 5 MiB sınırını aşıyor.")
    client = build_opener(HTTPCookieProcessor(http.cookiejar.CookieJar()), YerelYonlendirme())
    token = TokenParser()
    with client.open(base + "/Hesap/Giris", timeout=30) as response:
        token.feed(response.read().decode("utf-8"))
    if not token.token:
        raise ValueError("Giriş formu güvenlik anahtarı bulunamadı.")
    form = urlencode({"Input.Eposta": args.eposta, "Input.Sifre": getpass.getpass("Yönetici şifresi: "),
                      "__RequestVerificationToken": token.token}).encode()
    with client.open(Request(base + "/Hesap/Giris", data=form), timeout=30) as response:
        response.read()
    with client.open(base + "/api/icerik/antiforgery", timeout=30) as response:
        csrf = json.load(response)["token"]
    request = Request(base + "/api/icerik/aktar", data=payload,
                      headers={"Content-Type": "application/json", "X-CSRF-TOKEN": csrf})
    with client.open(request, timeout=120) as response:
        print(response.read().decode("utf-8"))


if __name__ == "__main__":
    try:
        main()
    except HTTPError as error:
        print(f"HTTP {error.code}: {error.read().decode('utf-8', errors='replace')}", file=sys.stderr)
        sys.exit(1)
    except (OSError, ValueError, URLError) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
