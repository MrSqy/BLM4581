using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EgitimPlatformu.Web.Services;

// /api/notebook/oturum WebSocket bağlantısının mesaj döngüsü. Sözleşme docs/NOTEBOOK_OTURUMU.md'de.
// İstemci bir "calistir" mesajıyla hücre grubunu yollar; hücreler Jupyter köprüsünde aynı
// çekirdekte sırayla çalışır, sonuçlar hücre hücre geri gönderilir. Aynı anda tek çalışma olur.
public sealed class NotebookOturumu(INotebookCalistirmaServisi servis, ILogger<NotebookOturumu> logger)
{
    public const int AzamiMesaj = 256 * 1024;
    public const int AzamiHucre = 32;
    public static readonly TimeSpan BostaKalmaSiniri = TimeSpan.FromMinutes(10);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly SemaphoreSlim yazmaKilidi = new(1, 1);

    public async Task YurutAsync(WebSocket soket, CancellationToken baglanti)
    {
        Task? calisma = null;
        CancellationTokenSource? durdurma = null;
        await GonderAsync(soket, new { tur = "hazir", cekirdek = "python3", azamiHucre = AzamiHucre }, baglanti);
        try
        {
            while (soket.State == WebSocketState.Open && !baglanti.IsCancellationRequested)
            {
                using var bosta = CancellationTokenSource.CreateLinkedTokenSource(baglanti);
                if (calisma is null || calisma.IsCompleted) bosta.CancelAfter(BostaKalmaSiniri);
                var (mesaj, kapat) = await OkuAsync(soket, bosta.Token);
                if (kapat is not null)
                {
                    await KapatAsync(soket, kapat.Value.Durum, kapat.Value.Aciklama);
                    break;
                }

                if (mesaj is null) continue;
                var istekId = Metin(mesaj.Value, "istekId");
                switch (Metin(mesaj.Value, "tur"))
                {
                    case "calistir":
                        if (calisma is { IsCompleted: false })
                        {
                            await HataAsync(soket, istekId, "mesgul", "Önceki çalışma bitmeden yeni hücre gönderilemez.", baglanti);
                            break;
                        }
                        if (!Hucreler(mesaj.Value, out var hucreler, out var sorun))
                        {
                            await HataAsync(soket, istekId, "gecersiz_istek", sorun, baglanti);
                            break;
                        }
                        durdurma?.Dispose();
                        durdurma = CancellationTokenSource.CreateLinkedTokenSource(baglanti);
                        calisma = CalistirAsync(soket, istekId, hucreler, durdurma.Token);
                        break;
                    case "durdur":
                        if (calisma is { IsCompleted: false }) await durdurma!.CancelAsync();
                        break;
                    case "ping":
                        await GonderAsync(soket, new { tur = "pong", istekId }, baglanti);
                        break;
                    default:
                        await HataAsync(soket, istekId, "gecersiz_istek", "Bilinmeyen mesaj türü.", baglanti);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (!baglanti.IsCancellationRequested)
        {
            await KapatAsync(soket, WebSocketCloseStatus.NormalClosure, "Oturum boşta kaldığı için kapatıldı.");
        }
        catch (WebSocketException ex)
        {
            logger.LogInformation(ex, "Notebook oturumu istemci tarafından koptu.");
        }
        finally
        {
            if (durdurma is not null) await durdurma.CancelAsync();
            if (calisma is not null) await calisma;
            durdurma?.Dispose();
        }
    }

    private async Task CalistirAsync(WebSocket soket, string? istekId, string[] hucreler, CancellationToken ct)
    {
        try
        {
            await GonderAsync(soket, new { tur = "basladi", istekId, hucreSayisi = hucreler.Length }, ct);
            var sonuc = await servis.CalistirAsync(hucreler, ct);
            for (var i = 0; i < sonuc.Hucreler.Length; i++)
                await GonderAsync(soket, new
                {
                    tur = "hucre", istekId, sira = i,
                    durum = sonuc.Hucreler[i].Durum, ciktilar = sonuc.Hucreler[i].Ciktilar
                }, ct);
            await GonderAsync(soket, new { tur = "bitti", istekId, durum = sonuc.Durum }, ct);
        }
        catch (OperationCanceledException)
        {
            if (soket.State == WebSocketState.Open)
                await GonderAsync(soket, new { tur = "bitti", istekId, durum = "durduruldu" }, CancellationToken.None);
        }
        catch (ArgumentException ex)
        {
            await HataAsync(soket, istekId, "gecersiz_istek", ex.Message, CancellationToken.None);
        }
        catch (InvalidOperationException ex)
        {
            // Sandbox kuyruğu dolu veya Docker yok: istemciye kalıcı olmayan bir hata olarak bildirilir.
            logger.LogWarning(ex, "Notebook çalıştırması sandbox'a ulaşamadı.");
            await GonderAsync(soket, new { tur = "bitti", istekId, durum = "altyapi_yok" }, CancellationToken.None);
        }
        catch (WebSocketException)
        {
            // Bağlantı çalışma sırasında koptu; sonuç gönderilecek kimse yok.
        }
    }

    private static bool Hucreler(JsonElement mesaj, out string[] hucreler, out string sorun)
    {
        hucreler = [];
        sorun = "";
        if (!mesaj.TryGetProperty("hucreler", out var dizi) || dizi.ValueKind != JsonValueKind.Array)
        {
            sorun = "'hucreler' bir metin dizisi olmalı.";
            return false;
        }
        if (dizi.GetArrayLength() is < 1 or > AzamiHucre)
        {
            sorun = $"Bir çalışmada 1 ile {AzamiHucre} arasında hücre gönderilebilir.";
            return false;
        }
        if (dizi.EnumerateArray().Any(h => h.ValueKind != JsonValueKind.String))
        {
            sorun = "'hucreler' yalnızca metin içermeli.";
            return false;
        }
        hucreler = dizi.EnumerateArray().Select(h => h.GetString()!).ToArray();
        return true;
    }

    private static async Task<(JsonElement? Mesaj, (WebSocketCloseStatus Durum, string Aciklama)? Kapat)> OkuAsync(
        WebSocket soket, CancellationToken ct)
    {
        var tampon = ArrayPool<byte>.Shared.Rent(16 * 1024);
        try
        {
            using var veri = new MemoryStream();
            ValueWebSocketReceiveResult sonuc;
            do
            {
                sonuc = await soket.ReceiveAsync(tampon.AsMemory(), ct);
                if (sonuc.MessageType == WebSocketMessageType.Close)
                    return (null, (WebSocketCloseStatus.NormalClosure, "Görüşmek üzere."));
                if (veri.Length + sonuc.Count > AzamiMesaj)
                    return (null, (WebSocketCloseStatus.MessageTooBig, "Mesaj 256 KB sınırını aşıyor."));
                veri.Write(tampon, 0, sonuc.Count);
            } while (!sonuc.EndOfMessage);

            if (sonuc.MessageType != WebSocketMessageType.Text)
                return (null, (WebSocketCloseStatus.InvalidMessageType, "Yalnızca JSON metin mesajları kabul edilir."));
            try
            {
                using var belge = JsonDocument.Parse(veri.ToArray());
                if (belge.RootElement.ValueKind == JsonValueKind.Object) return (belge.RootElement.Clone(), null);
            }
            catch (JsonException) { }
            return (null, (WebSocketCloseStatus.InvalidPayloadData, "Mesaj geçerli bir JSON nesnesi değil."));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(tampon);
        }
    }

    private Task HataAsync(WebSocket soket, string? istekId, string kod, string mesaj, CancellationToken ct) =>
        GonderAsync(soket, new { tur = "hata", istekId, kod, mesaj }, ct);

    private async Task GonderAsync(WebSocket soket, object mesaj, CancellationToken ct)
    {
        var veri = JsonSerializer.SerializeToUtf8Bytes(mesaj, Json);
        await yazmaKilidi.WaitAsync(ct);
        try
        {
            if (soket.State == WebSocketState.Open)
                await soket.SendAsync(veri, WebSocketMessageType.Text, endOfMessage: true, ct);
        }
        finally
        {
            yazmaKilidi.Release();
        }
    }

    private async Task KapatAsync(WebSocket soket, WebSocketCloseStatus durum, string aciklama)
    {
        await yazmaKilidi.WaitAsync();
        try
        {
            if (soket.State is WebSocketState.Open or WebSocketState.CloseReceived)
                await soket.CloseAsync(durum, aciklama, CancellationToken.None);
        }
        catch (WebSocketException) { }
        finally
        {
            yazmaKilidi.Release();
        }
    }

    private static string? Metin(JsonElement mesaj, string alan) =>
        mesaj.TryGetProperty(alan, out var deger) && deger.ValueKind == JsonValueKind.String ? deger.GetString() : null;
}
