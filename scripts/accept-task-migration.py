"""Yalnız yeni, geçici PostgreSQL konteynerinde migration/aktarım kabulü. Yerel DB'ye bağlanmaz."""
import json
import os
from pathlib import Path
import secrets
import subprocess
import time
import uuid

ROOT=Path(__file__).resolve().parent.parent
name='blm-task-migration-'+uuid.uuid4().hex[:10]
password=secrets.token_urlsafe(24)
env={**os.environ,'POSTGRES_PASSWORD':password}
created=False

def run(args, env=None):
    result=subprocess.run(args,cwd=ROOT,env=env,capture_output=True,text=True,timeout=90)
    if result.returncode: raise RuntimeError(f'{args[0]} başarısız:\n{result.stdout[-1500:]}\n{result.stderr[-1500:]}')
    return result.stdout

try:
    run(['docker','run','--pull=never','--rm','-d','--name',name,'-e','POSTGRES_PASSWORD','-p','127.0.0.1::5432','postgres:16.15'],env)
    created=True
    for _ in range(40):
        result=subprocess.run(['docker','exec',name,'pg_isready','-U','postgres'],capture_output=True)
        if result.returncode==0: break
        time.sleep(.25)
    else: raise RuntimeError('Geçici PostgreSQL başlamadı.')
    port=run(['docker','port',name,'5432/tcp']).strip().rsplit(':',1)[1]
    config={**os.environ,'ConnectionStrings__Varsayilan':f'Host=127.0.0.1;Port={port};Database=postgres;Username=postgres;Password={password}', 'ASPNETCORE_ENVIRONMENT':'Testing'}
    ef=str(Path.home()/'.dotnet/tools/dotnet-ef')
    run([ef,'database','update','UyelikVeRoller','--project','src/EgitimPlatformu.Web','--no-build'],config)
    def sql(query):
        return run(['docker','exec',name,'psql','-U','postgres','-d','postgres','-At','-v','ON_ERROR_STOP=1','-c',query]).strip()
    sql('''INSERT INTO "AspNetUsers" ("Id","UserName","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount") VALUES ('korunan','fixture',false,false,false,false,0); UPDATE "Gorevler" SET "Aciklama"='KORUNAN ESKI SEKIZINCI' WHERE "EgitimId"=1 AND "No"=8;''')
    before=sql('SELECT "Id"||\':\'||"Aciklama" FROM "Gorevler" WHERE "EgitimId"=1 AND "No"=8;')
    run([ef,'database','update','GorevBazliKontrol','--project','src/EgitimPlatformu.Web','--no-build'],config)
    assert sql('SELECT "Id"||\':\'||"Aciklama" FROM "Gorevler" WHERE "EgitimId"=1 AND "No"=8;')==before
    assert sql('SELECT "Aktif" FROM "Gorevler" WHERE "EgitimId"=1 AND "No"=8;')=='t'
    app=['dotnet','run','--no-build','--no-restore','--no-launch-profile','--project','src/EgitimPlatformu.Web','--']
    package=str(ROOT/'docs/ornekler/py01-v2.json')
    plan=run(app+['--icerik-planla',package],config)
    assert 'pasifleşecek numaralar: 8' in plan,plan
    assert sql('SELECT "Aktif" FROM "Gorevler" WHERE "EgitimId"=1 AND "No"=8;')=='t'
    for _ in range(2): run(app+['--icerik-aktar',package],config)
    assert sql('SELECT "Id"||\':\'||"Aciklama" FROM "Gorevler" WHERE "EgitimId"=1 AND "No"=8;')==before
    assert sql('SELECT "Aktif" FROM "Gorevler" WHERE "EgitimId"=1 AND "No"=8;')=='f'
    assert sql('SELECT count(*) FROM "Gorevler" WHERE "EgitimId"=1 AND "Aktif";')=='7'
    assert sql('SELECT count(*) FROM "Gorevler" WHERE "EgitimId"=1;')=='8'
    assert sql('SELECT count(*) FROM "AspNetUsers" WHERE "Id"=\'korunan\';')=='1'
    assert sql('SELECT count(*) FROM "Gorevler" WHERE "EgitimId"=1 AND "Tur"=\'kod\';')=='5'
    print('PASS gerçek PostgreSQL: schema migration, salt okunur plan, iki aktarım, 7 aktif/8 toplam, eski 8. görev metni/kimliği ve kullanıcı korundu.')
finally:
    if created:
        subprocess.run(['docker','rm','-f',name],stdout=subprocess.DEVNULL,check=True)
