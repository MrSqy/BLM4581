# Notebook kayıt API’si

API, `Authorization: Bearer <JWT>` ile çağrılır. Kişisel notebook’u sahibi okur ve yönetir. Derse bağlı notebook’u oturumlu kullanıcılar okuyabilir; yönetici ekleyebilir, güncelleyebilir ve silebilir.

| Yöntem ve yol | Sonuç |
|---|---|
| `GET /api/notebooklar?egitimId=1&sayfa=1&sayfaBoyutu=20` | Ders notebook’larının özetleri. `egitimId` isteğe bağlıdır. `kisisel=true` yalnız kişisel kayıtları getirir. |
| `GET /api/notebooklar/{id}` | Özet alanları ve `icerik` nesnesi. |
| `POST /api/notebooklar` | `baslik`, `icerik` ve isteğe bağlı `egitimId` ile oluşturur. `201` ve `Location` döner. |
| `PUT /api/notebooklar/{id}` | `baslik`, `icerik` ve son okunan `surum` ile günceller. `204` döner. |
| `DELETE /api/notebooklar/{id}` | İzinli kaydı siler ve `204` döner. |

`icerik` alanına nbformat 4 belgesi nesne olarak verilir. API, hücreler ile üst düzey metadata ve ek alanları saklar. Bu aşamada doğrulama belge zarfı ve temel türler düzeyindedir. Özetler `id`, `baslik`, `egitimId`, `surum` ve `guncellendiUtc` içerir.

Eski sürümle yazma isteği `409` verir. İstemci kaydı yeniden açıp güncel sürümü alır. Erişim kapsamı dışındaki kayıt ayrıntı ve yazma uçlarında `404` döner. Öğrencinin derse bağlı notebook oluşturması `403` döner. Ders silinirse notebook kişisel kayda döner ve sahibi erişmeye devam eder.

Notebook kayıtları için mevcut EF migration zincirini uygulayın. Yönetici, **Yönetim → Notebook’lar** sayfasından boş bir notebook açabilir veya derse `.ipynb` dosyası ekleyebilir. Oturumlu öğrenciler dersten notebook’a geçer. Hücre sırası, kaynak düzenleme ve Markdown önizleme notebook sayfasındadır. Dosyadaki kayıtlı çıktılar ve metadata düzenleme sırasında korunur.

## Mobil yönetim

- `POST /api/mobil/yonetim/egitimler` ve `GET|PUT|DELETE /api/mobil/yonetim/egitimler/{id}` yalnız yönetici Bearer tokenıyla çalışır. Düzenleme detayında eski pytest kodu bulunur.
- `GET|POST /api/mobil/yonetim/egitimler/{id}/gorevler` ve `PUT|DELETE /api/mobil/yonetim/egitimler/{id}/gorevler/{gorevId}` aynı yönetici politikasını kullanır. Görev silme çağrısı görev kimliğini ve geçmiş bağlantılarını koruyarak görevi pasifleştirir. Düzenleme ile yeniden etkinleştirilebilir.

## Mobil notebook WebSocket

Mobil istemci kayıtları `GET /api/mobil/notebooklar` üzerinden seçer. `GET /api/mobil/notebook-oturumu?notebookId=1` isteği Bearer tokenı ve WebSocket yükseltmesiyle açılır. Notebook kimliği isteğe bağlıdır. Sunucu ilk mesajda `tur: "baglandi"`, `oturumId` ve `notebookId` gönderir. `{"tur":"ping"}` mesajına aynı oturum kimliğiyle `pong` döner. Bağlantı en fazla 10 dakika, JWT’nin geçerli olduğu sürece ve 75 saniyelik mesaj bekleme sınırı içinde yaşar. Canlı hücre çalıştırma komutları 7. hafta kapsamındadır.

## Artımlı içerik karşılaştırması

`POST /api/icerik/fark`, normal aktarım paketini yönetici çerezi ve `X-CSRF-TOKEN` ile kabul eder. Kategori, ders ve görev alanlarını karşılaştırır, `degisenDersler` dizisinde yeni veya değişmiş ders kodlarını döner. Kayıt işlemi mevcut `POST /api/icerik/aktar` üzerinden yapılır.
