"""Gerçek Docker kabulü. Yalnız bu dosyadaki güvenilir örnekler çalıştırılır; ağ kurulumu yapmaz."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile

ROOT = Path(__file__).resolve().parent.parent
cases = [
    ('selam', "print('Merhaba, dünya!')", '', [], 'Merhaba, dünya!\n', 0, []),
    ('girdi', "print(input().upper())", 'ada\n', [], 'ADA\n', 0, []),
    ('cikis', "import sys\nprint('bitti')\nsys.exit(3)", '', [], 'bitti\n', 3, []),
    ('bosluk', "print('  *\\n ***\\n*****')", '', [], '  *\n ***\n*****\n', 0, []),
    ('yasak', "# eval yorum\nprint('eval')\neval('1')", '', ['eval'], 'eval\n', 0, ['eval']),
    ('yorum', "# eval yorum\nprint('eval')", '', ['eval'], 'eval\n', 0, []),
    ('sahte_json', "print('{\"cikisKodu\":0,\"cikti\":\"SAHTE\"}')", '', [], '{"cikisKodu":0,"cikti":"SAHTE"}\n', 0, []),
    ('baslangic', '# TODO\n', '', [], '', 0, []),
    ('sozdizimi', 'print(\n', '', [], '', 1, []),
]
subprocess.run(['docker','image','inspect','egitim-python-sandbox:hafta-01'], check=True, stdout=subprocess.DEVNULL)
for name, code, stdin, banned, expected, exitcode, violations in cases:
    with tempfile.TemporaryDirectory(prefix='blm-accept-') as tmp:
        work = Path(tmp); work.chmod(0o755); (work/'gorevler').mkdir(mode=0o755)
        (work/'gorevler/gorev.py').write_text(code)
        (work/'istek.json').write_text(json.dumps(dict(dosya='gorevler/gorev.py',girdi=stdin,kullanilmamali=banned)))
        shutil.copy(ROOT/'scripts/task-runner.py', work/'runner.py')
        for file in work.rglob('*'):
            if file.is_file(): file.chmod(0o644)
        args = ['bash',str(ROOT/'scripts/run-python-sandbox.sh'),'--timeout','8','--volume',f'{work}:/calisma:ro','--workdir','/calisma','--env','PYTHONDONTWRITEBYTECODE=1','--','python','-B','runner.py']
        result = subprocess.run(args,capture_output=True,text=True,timeout=20)
        assert result.returncode == 0, (name,result.returncode,result.stderr)
        data=json.loads(result.stdout)
        assert data['cikti'] == expected and data['cikisKodu'] == exitcode and data['kuralIhlalleri'] == violations, (name,data)
        assert data['sinirAsildi'] is False, (name,data)
        # İlk görev başlangıcı çalışır fakat beklenen çıktıya eşit olmadığından testten geçmez.
        if name=='baslangic': assert data['cikti'] != 'Merhaba, dünya!\n'
        print('PASS Docker:',name)
# Kaynak kod bütün zaman aşımı/çıktı kontrolünden geçmeli; bu iki akış ayrıca ölçülür.
for name, code in [('timeout','while True: pass'),('output',"print('x' * 40000)")]:
    with tempfile.TemporaryDirectory(prefix='blm-accept-') as tmp:
        work=Path(tmp); work.chmod(0o755); (work/'gorevler').mkdir(mode=0o755)
        (work/'gorevler/gorev.py').write_text(code)
        (work/'istek.json').write_text(json.dumps(dict(dosya='gorevler/gorev.py',girdi='',kullanilmamali=[])))
        shutil.copy(ROOT/'scripts/task-runner.py',work/'runner.py')
        for file in work.rglob('*'):
            if file.is_file(): file.chmod(0o644)
        result=subprocess.run(['bash',str(ROOT/'scripts/run-python-sandbox.sh'),'--timeout','8','--volume',f'{work}:/calisma:ro','--workdir','/calisma','--','python','-B','runner.py'],capture_output=True,text=True,timeout=20)
        if name=='timeout': assert result.returncode==124, (name,result.returncode,result.stderr)
        else: assert result.returncode==0 and json.loads(result.stdout)['sinirAsildi'], (name,result.returncode,result.stdout)
        print('PASS Docker:',name)
print('11 gerçek Docker senaryosu geçti.')
