# Üyelik ve roller

Nisa'nın `IHesapServisi` sözleşmesi `IdentityHesapServisi` ile uygulanır.
Kayıt/giriş/çıkış sayfaları aynı adreslerde kalır. EF Core Identity tabloları
`UyelikVeRoller` migration'ıyla mevcut PostgreSQL veritabanına eklenir;
kategori, ders ve görev tabloları/verileri korunur.

## İlk hazırlık (Linux)

Depo kökünde önce migration'ı uygulayın:

```bash
dotnet ef database update --project src/EgitimPlatformu.Web
```

Başlangıç yöneticisi için e-posta ve şifreyi terminalde okuyup yalnızca komutun
ortamına aktarın. Değerler depoya yazılmaz; şifre terminal geçmişine girmez:

```bash
read 'yonetici_eposta?Yönetici e-postası: '
read -s 'yonetici_sifre?Yönetici şifresi: '
echo
BaslangicYoneticisi__Eposta="$yonetici_eposta" BaslangicYoneticisi__Sifre="$yonetici_sifre" dotnet run --no-launch-profile --project src/EgitimPlatformu.Web -- --identity-hazirla
unset yonetici_eposta yonetici_sifre
```

Yukarıdaki `read` biçimi bu çalışma ortamındaki **Zsh** içindir. Bash'te ilk iki
satır yerine `read -r -p 'Yönetici e-postası: ' yonetici_eposta` ve
`read -r -s -p 'Yönetici şifresi: ' yonetici_sifre` kullanılır.

Alternatif olarak aynı iki ayar `BaslangicYoneticisi:Eposta` ve
`BaslangicYoneticisi:Sifre` adlarıyla user-secrets üzerinden sağlanabilir.
Bağlantı dizesi README'deki gibi `ConnectionStrings:Varsayilan` ayarından okunur.
Bu hazırlık komutu web sunucusunu başlatmadan çıkar; normal web başlangıcı
migration, rol veya yönetici hesabı oluşturmaz.

Hazırlık önce `Yonetici` ve `Ogrenci` rollerini, ardından yönetici hesabını
oluşturur. Yeniden çalıştırmak aynı yöneticiye dokunmaz ve şifresini değiştirmez.
E-posta önceden Öğrenci olarak kayıtlıysa hesap otomatik yükseltilmez; komut
hata verir ve transaction geri alınır. Yeni yönetici için farklı e-posta seçin.
Hazırlığı normal kayıt kabul etmeye başlamadan önce çalıştırın.

```bash
dotnet run --project src/EgitimPlatformu.Web --launch-profile http
```

## Hesap davranışı

- Normal kayıtlar yalnız `Ogrenci` rolü alır; istemci rol seçemez.
- Parolalar Identity tarafından özetlenir. En az 8 karakter, büyük/küçük harf,
  rakam ve özel karakter gerekir. Aynı e-posta tekrar kaydedilemez.
- Beş başarısız giriş hesabı beş dakika kilitler. Giriş hatası e-posta varlığı,
  yanlış parola veya kilit ayrıntısını açıklamaz.
- Oturum Identity çereziyle taşınır; kimlik doğrulama yetkilendirmeden önce çalışır.
  Çerez HttpOnly ve SameSite=Lax'tır; HTTPS isteğinde Secure olur.
- Çıkış yalnız antiforgery doğrulamalı POST ile yapılır.
- Kayıt, Öğrenci rolü atamasıyla aynı transaction'dadır; rol ataması başarısızsa
  kullanıcı kaydı da geri alınır.
- E-posta doğrulama/gönderme ve parola sıfırlama bu haftanın kapsamına dahil değildir.

## Kanıt sınırı

`IdentityTests`, gerçek UserManager/SignInManager ve gerçek oturum çerezi kullanır;
veritabanı test için bellekte SQLite'tır. Nisa'nın mevcut testleri kendi taklit
hesap servisiyle korunur. PostgreSQL migration ve gerçek sunucu denemesi ayrıca
kaydedilmelidir; SQLite testi PostgreSQL kabulü yerine geçmez.
