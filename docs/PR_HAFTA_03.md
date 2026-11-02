## Değişiklik

Mobil ve genel API'nin çakışan Bearer kayıtları tek JWT altyapısında birleştirildi. Mobil kayıt/giriş yanıtı korundu; çıkış, rol değişikliği ve hesap kilidi tokenları geçersizleştiriyor. Misafirler yalnız kategori içindeki ilk dersin detayına ve kod uçlarına erişebiliyor; genel detay API'si bu sınırı aşamıyor.

Eğitim CRUD API'si DTO doğrulaması, yönetici yetkisi, filtreleme ve sayfalama ile eklendi. Development Swagger, mobil runner bağlantısı ve API/Flutter devir belgeleri eklendi. OpenAPI bağımlılığı 2.12.0'a yükseltildi.

## Doğrulama

102 .NET testi ve 5 Python fixture testi geçti. Son derleme 0 hata, 0 uyarı verdi (yalnız yerel önbellekle çözümleme; yeni ağ güvenlik taraması yok). .NET uygulama/Identity/JWT entegrasyonu SQLite ile; runner hata yolları taklit süreçle; beş güvenilir Python fixture'ı yerelde test edildi. Gerçek PostgreSQL, Docker sandbox ve telefon kabulü bu değişiklikte yapılmadı. Docker imajı yerelde bulunmuyor. Ayrıntılar docs/API_HAFTA_03.md belgesinde.
