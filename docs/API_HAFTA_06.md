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

Notebook kayıtları için mevcut EF migration zincirini uygulayın. Bu sözleşme depolamayı kapsar. Çekirdek oturumu ve WebSocket ayrı entegrasyon adımlarıdır.
