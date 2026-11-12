namespace EgitimPlatformu.Web.Services;

// Sorgular, oturumdan alınan kullanıcı kimliğiyle sınırlandırılır.
public interface IGonderimGecmisi
{
    bool Kullanilabilir { get; }
    Task<GonderimSayfasi> ListeAsync(string kullaniciId, int sayfa, int boyut, CancellationToken ct);
    Task<GonderimGorunumu?> DetayAsync(string kullaniciId, int id, CancellationToken ct);
}
public sealed record GonderimGorunumu(int Id, string KullaniciId, DateTimeOffset Tarih,
    int EgitimId, string Ders, string? Gorev, string Durum, string Kod, string Cikti, string Hata)
{
    public int? GorevId { get; init; }
    public int? GorevNo { get; init; }
    public bool Basarili { get; init; }
    public int? CikisKodu { get; init; }
    public long SureMs { get; init; }
}
public sealed record GonderimSayfasi(int Toplam, IReadOnlyList<GonderimGorunumu> Kayitlar);
