namespace EgitimPlatformu.Web.Services;

// Yalnız 4.2 web görünüm sözleşmesi; 4.1'in DB modeli veya API DTO'su değildir.
// Gerçek adaptör tüm sorguları sunucudan alınan kullaniciId ile sınırlandırmalıdır.
public interface IGonderimGecmisi
{
    bool Kullanilabilir { get; }
    Task<GonderimSayfasi> ListeAsync(string kullaniciId, int sayfa, int boyut, CancellationToken ct);
    Task<GonderimGorunumu?> DetayAsync(string kullaniciId, int id, CancellationToken ct);
}
public sealed record GonderimGorunumu(int Id, string KullaniciId, DateTimeOffset Tarih,
    int EgitimId, string Ders, string? Gorev, string Durum, string Kod, string Cikti, string Hata);
public sealed record GonderimSayfasi(int Toplam, IReadOnlyList<GonderimGorunumu> Kayitlar);

public sealed class BekleyenGonderimGecmisi : IGonderimGecmisi
{
    public bool Kullanilabilir => false;
    public Task<GonderimSayfasi> ListeAsync(string kullaniciId, int sayfa, int boyut, CancellationToken ct)
        => throw new InvalidOperationException("4.1 gönderim altyapısı henüz bağlı değil.");
    public Task<GonderimGorunumu?> DetayAsync(string kullaniciId, int id, CancellationToken ct)
        => throw new InvalidOperationException("4.1 gönderim altyapısı henüz bağlı değil.");
}
