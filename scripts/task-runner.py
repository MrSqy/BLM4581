"""Sadece mevcut Docker sandbox içinde çalıştırılır. Beklenen sonuçlar burada bulunmaz."""
import io
import json
import os
from pathlib import Path
import re
import resource
import signal
import subprocess
import sys
import tempfile
import tokenize

LIMIT = 24000

def ihlaller(kod, yasaklar):
    # Kaynağın kuralı gibi yorum ve dizgiler kontrol dışıdır; satırlar korunur.
    masked = list(kod)
    offsets = [0]
    for line in kod.splitlines(keepends=True):
        offsets.append(offsets[-1] + len(line))
    fstart = None
    try:
        for token in tokenize.generate_tokens(io.StringIO(kod).readline):
            if token.type == getattr(tokenize, 'FSTRING_START', -1):
                fstart = offsets[token.start[0]-1] + token.start[1]
            if token.type in (tokenize.STRING, tokenize.COMMENT) or token.type == getattr(tokenize, 'FSTRING_END', -1):
                start = fstart if fstart is not None else offsets[token.start[0]-1] + token.start[1]
                end = offsets[token.end[0]-1] + token.end[1]
                for n in range(start, end):
                    if masked[n] != '\n': masked[n] = ' '
                fstart = None
    except (tokenize.TokenError, IndentationError):
        pass  # Python çalıştırması sözdizimi hatasını ayrıca raporlar.
    text = ''.join(masked)
    return [word for word in yasaklar if re.search(r'(?<!\w)' + re.escape(word) + r'(?!\w)', text)]

def limits():
    resource.setrlimit(resource.RLIMIT_FSIZE, (32768, 32768))

request = json.loads(Path('istek.json').read_text())
code = Path(request['dosya']).read_text()
rules = ihlaller(code, request['kullanilmamali'])
with tempfile.TemporaryDirectory(dir='/tmp') as work:
    with tempfile.TemporaryFile() as out, tempfile.TemporaryFile() as err:
        # Öğrenci stdout'u JSON protokolünden ayrıdır; sahte sonuç satırı üretmek sonucu değiştirmez.
        process = subprocess.Popen([sys.executable, '-B', '-u', str(Path(request['dosya']).resolve())],
            cwd=work, stdin=subprocess.PIPE, stdout=out, stderr=err, start_new_session=True, preexec_fn=limits)
        try:
            process.communicate(request['girdi'].encode(), timeout=5)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
            process.wait()
            sys.exit(124)
        finally:
            # Arka planda kalan çocuklar aynı süreç grubundadır.
            try: os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError: pass
        out.seek(0); stdout = out.read(32769)
        err.seek(0); stderr = err.read(32769)
        print(json.dumps(dict(cikti=stdout[:LIMIT].decode('utf-8', 'replace'),
            hata=stderr[:LIMIT].decode('utf-8', 'replace'), cikisKodu=process.returncode,
            sinirAsildi=len(stdout) > LIMIT or len(stderr) > LIMIT, kuralIhlalleri=rules)))
