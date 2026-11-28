# Notebook oturumu (WebSocket)

Mobil uygulama notebook hücrelerini bu uçla çalıştırır (BLM4587 6.3). Hücreler 5.4'teki
Jupyter köprüsüne verilir: sandbox konteynerinde bir çekirdek açılır, hücreler aynı çekirdekte
sırayla çalışır ve çıktıları nbformat 4 biçiminde döner.

```
GET /api/notebook/oturum
Authorization: Bearer <accessToken>
Upgrade: websocket
```

- Token `/api/hesap/giris` veya `/api/mobil/hesap/giris` yanıtından alınır. Token yoksa ya da geçersizse el sıkışma `401` ile reddedilir.
- WebSocket olmayan bir istek `400` alır.
- Mesajların tamamı UTF-8 JSON metnidir; bir mesaj en fazla 256 KB olabilir.
- Sunucu 30 saniyede bir ping gönderir. 10 dakika boyunca mesaj gelmezse oturumu kapatır.

## İstemciden sunucuya

| `tur` | Alanlar | Anlamı |
|---|---|---|
| `calistir` | `istekId`, `hucreler: string[]` (1–32) | Hücreleri yeni bir çekirdekte sırayla çalıştırır. |
| `durdur` | — | Süren çalışmayı durdurur. Konteyner kapatılır. |
| `ping` | `istekId` | Sunucu `pong` ile yanıt verir. |

Her `calistir` yeni bir çekirdekte çalışır. Bir hücre önceki hücrelerin tanımladığı değişkenlere
ihtiyaç duyuyorsa istemci o hücreye kadar olan bütün kod hücrelerini birlikte gönderir. Çekirdeğin
çalışmalar arasında açık kalması 7.3'te ele alınacak.

## Sunucudan istemciye

| `tur` | Alanlar | Ne zaman |
|---|---|---|
| `hazir` | `cekirdek`, `azamiHucre` | Bağlantı açılınca. |
| `basladi` | `istekId`, `hucreSayisi` | Çalışma kuyruğa alınınca. |
| `hucre` | `istekId`, `sira`, `durum` (`ok` / `error` / `aborted`), `ciktilar` | Her hücre için, sırayla. |
| `bitti` | `istekId`, `durum` | Çalışma bitince. |
| `pong` | `istekId` | `ping` yanıtı. |
| `hata` | `istekId`, `kod`, `mesaj` | İstek işlenemeyince. Oturum açık kalır. |

`bitti.durum` değerleri:

- `tamamlandi`: bütün hücreler çalıştı.
- `cikti_siniri`: çıktı 48 KB'ı aştı; o ana kadarki hücreler gönderildi.
- `zaman_asimi`: çalışma 20 saniyeyi aştı.
- `durduruldu`: istemci `durdur` gönderdi.
- `altyapi_yok`: Docker ya da Jupyter imajı kullanılamıyor.

`hata.kod` değerleri:

- `gecersiz_istek`: alanlar eksik, yanlış türde ya da sınır dışında.
- `mesgul`: önceki çalışma sürüyor.

`ciktilar` dizisindeki her öğe bir nbformat çıktısıdır:
`stream` (`name`, `text`), `execute_result` / `display_data` (`data`, `metadata`)
ve `error` (`ename`, `evalue`, `traceback`).

## Örnek

```json
→ {"tur":"calistir","istekId":"a1","hucreler":["x = 40","print(x + 2)"]}
← {"tur":"basladi","istekId":"a1","hucreSayisi":2}
← {"tur":"hucre","istekId":"a1","sira":0,"durum":"ok","ciktilar":[]}
← {"tur":"hucre","istekId":"a1","sira":1,"durum":"ok","ciktilar":[{"output_type":"stream","name":"stdout","text":"42\n"}]}
← {"tur":"bitti","istekId":"a1","durum":"tamamlandi"}
```
