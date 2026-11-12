namespace EgitimPlatformu.Web.Services;

// 4.4: Bütün kod çalıştırma yolları (/api/calistir, görev ve mobil uçları, ders sayfası editörü)
// sandbox'a IHariciIslemCalistirici üzerinden gider. Kuyruk bu noktada durduğu için toplam
// konteyner sayısı tek yerden sınırlanır.
public sealed record SandboxKuyruguAyarlari(
    int EsZamanliCalisma = 2,
    int BekleyenSiniri = 8,
    int BeklemeSaniye = 15,
    int IslemUstSiniriSaniye = 30)
{
    public static SandboxKuyruguAyarlari Oku(IConfiguration configuration)
    {
        var bolum = configuration.GetSection("Sandbox");
        var varsayilan = new SandboxKuyruguAyarlari();
        var ayarlar = new SandboxKuyruguAyarlari(
            bolum.GetValue(nameof(EsZamanliCalisma), varsayilan.EsZamanliCalisma),
            bolum.GetValue(nameof(BekleyenSiniri), varsayilan.BekleyenSiniri),
            bolum.GetValue(nameof(BeklemeSaniye), varsayilan.BeklemeSaniye),
            bolum.GetValue(nameof(IslemUstSiniriSaniye), varsayilan.IslemUstSiniriSaniye));
        if (ayarlar.EsZamanliCalisma < 1 || ayarlar.BekleyenSiniri < 0 ||
            ayarlar.BeklemeSaniye < 1 || ayarlar.IslemUstSiniriSaniye < 1)
            throw new InvalidOperationException("Sandbox kuyruğu ayarları geçersiz.");
        return ayarlar;
    }
}

// InvalidOperationException'dan türer: mevcut "sandbox kullanılamıyor" yakalayıcıları 503 vermeye devam eder.
public sealed class SandboxMesgulException(string message) : InvalidOperationException(message);

public sealed class KuyrukluIslemCalistirici(IHariciIslemCalistirici ic, SandboxKuyruguAyarlari ayarlar)
    : IHariciIslemCalistirici
{
    private readonly SemaphoreSlim yuvalar = new(ayarlar.EsZamanliCalisma, ayarlar.EsZamanliCalisma);
    private int kabulEdilen;

    public int Calisan => ayarlar.EsZamanliCalisma - yuvalar.CurrentCount;
    public int Bekleyen => Math.Max(0, Volatile.Read(ref kabulEdilen) - Calisan);

    public async Task<KodCalistirmaSonucu> CalistirAsync(string dosyaAdi, IReadOnlyList<string> argumanlar,
        CancellationToken cancellationToken = default)
    {
        // Çalışan + bekleyen toplamı sınırı aşarsa istek kuyruğa hiç girmez.
        if (Interlocked.Increment(ref kabulEdilen) > ayarlar.EsZamanliCalisma + ayarlar.BekleyenSiniri)
        {
            Interlocked.Decrement(ref kabulEdilen);
            throw new SandboxMesgulException("Sandbox kuyruğu dolu.");
        }

        try
        {
            if (!await yuvalar.WaitAsync(TimeSpan.FromSeconds(ayarlar.BeklemeSaniye), cancellationToken))
                throw new SandboxMesgulException("Sandbox sırası beklenirken süre doldu.");
            try
            {
                return await UstSinirlaCalistirAsync(dosyaAdi, argumanlar, cancellationToken);
            }
            finally
            {
                yuvalar.Release();
            }
        }
        finally
        {
            Interlocked.Decrement(ref kabulEdilen);
        }
    }

    // Betiğin kendi süre sınırı konteynerin içini kapsar. Docker'ın kendisi takılırsa
    // (create/start/inspect yanıt vermezse) bu üst sınır işlemi durdurur ve yuvayı boşaltır.
    private async Task<KodCalistirmaSonucu> UstSinirlaCalistirAsync(string dosyaAdi,
        IReadOnlyList<string> argumanlar, CancellationToken cancellationToken)
    {
        using var ustSinir = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ustSinir.CancelAfter(TimeSpan.FromSeconds(ayarlar.IslemUstSiniriSaniye));
        try
        {
            return await ic.CalistirAsync(dosyaAdi, argumanlar, ustSinir.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new KodCalistirmaSonucu(125, "",
                $"Sandbox {ayarlar.IslemUstSiniriSaniye} saniye içinde yanıt vermedi ve durduruldu.");
        }
    }
}
