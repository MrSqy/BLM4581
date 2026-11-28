using System.Text.RegularExpressions;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EgitimPlatformu.Web.Pages.Notebook;

// 6.4 Notebook sayfası v1. Notebook'un tamamı formda .ipynb olarak taşınır; her işlem
// (ekle, sil, taşı, önizle) sunucuda modele uygulanıp sayfa yeniden çizilir. Notebooklar
// tablosu (6.1) gelince aynı model kayıtlı notebook'tan doldurulacak.
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public partial class IndexModel(MarkdownServisi markdown) : PageModel
{
    public const int AzamiHucre = 200;

    public NotebookBelgesi Belge { get; private set; } = new();
    public string BelgeJson { get; private set; } = "";
    public string? Mesaj { get; private set; }
    public string? Hata { get; private set; }

    // Bir önceki işlemden sonra odaklanacak hücre.
    public int? Odak { get; private set; }

    [BindProperty] public string? Ipynb { get; set; }
    [BindProperty] public List<string>? Kaynaklar { get; set; }

    public void OnGet() => Yukle(Ornek());

    public IActionResult OnPostEkle(int sira, string tur)
    {
        if (!FormuOku()) return Page();
        if (Belge.Hucreler.Count >= AzamiHucre) return Uyari($"Bir notebook en fazla {AzamiHucre} hücre içerebilir.");
        var hucreTuru = tur == "markdown" ? HucreTuru.Markdown : HucreTuru.Kod;
        var yer = Math.Clamp(sira, 0, Belge.Hucreler.Count);
        Belge.Hucreler.Insert(yer, NotebookHucresi.Yeni(hucreTuru));
        return Tamam(yer, hucreTuru == HucreTuru.Markdown ? "Markdown hücresi eklendi." : "Kod hücresi eklendi.");
    }

    public IActionResult OnPostSil(int sira)
    {
        if (!FormuOku()) return Page();
        if (!Gecerli(sira)) return Uyari("Silinecek hücre bulunamadı.");
        Belge.Hucreler.RemoveAt(sira);
        if (Belge.Hucreler.Count == 0) Belge.Hucreler.Add(NotebookHucresi.Yeni(HucreTuru.Kod));
        return Tamam(Math.Min(sira, Belge.Hucreler.Count - 1), "Hücre silindi.");
    }

    public IActionResult OnPostTasi(int sira, int yon)
    {
        if (!FormuOku()) return Page();
        var hedef = sira + Math.Sign(yon);
        if (!Gecerli(sira) || !Gecerli(hedef) || yon == 0) return Uyari("Hücre bu yönde taşınamaz.");
        (Belge.Hucreler[sira], Belge.Hucreler[hedef]) = (Belge.Hucreler[hedef], Belge.Hucreler[sira]);
        return Tamam(hedef, "Hücre taşındı.");
    }

    public IActionResult OnPostTur(int sira)
    {
        if (!FormuOku()) return Page();
        if (!Gecerli(sira) || Belge.Hucreler[sira].Tur == HucreTuru.Ham) return Uyari("Hücre türü değiştirilemedi.");
        var hucre = Belge.Hucreler[sira];
        hucre.Tur = hucre.Tur == HucreTuru.Kod ? HucreTuru.Markdown : HucreTuru.Kod;
        hucre.Ciktilar.Clear();
        hucre.CalistirmaSayaci = null;
        return Tamam(sira, "Hücre türü değiştirildi.");
    }

    public IActionResult OnPostOnizle() => FormuOku() ? Tamam(null, "Önizleme güncellendi.") : Page();

    public IActionResult OnPostSifirla()
    {
        Yukle(Ornek());
        Mesaj = "Örnek notebook yeniden yüklendi.";
        return Page();
    }

    public string MarkdownHtml(NotebookHucresi hucre) => markdown.Html(hucre.Kaynak);

    // Kaydedilmiş çıktılardaki ANSI renk kodları düz metinde gösterilmez.
    public static string AnsiTemizle(string metin) => AnsiKodu().Replace(metin, "");

    // Yalnızca base64 karakterleri içeren PNG/JPEG data URI'si üretilir.
    public static string? GorselAdresi(NotebookCiktisi cikti)
    {
        foreach (var mime in new[] { "image/png", "image/jpeg" })
        {
            var veri = string.Concat((cikti.MetinVerisi(mime) ?? "").Where(c => !char.IsWhiteSpace(c)));
            if (veri.Length > 0 && Base64.IsMatch(veri)) return $"data:{mime};base64,{veri}";
        }
        return null;
    }

    private bool FormuOku()
    {
        try
        {
            Belge = IpynbDonusturucu.Oku(Ipynb ?? "");
        }
        catch (NotebookBicimHatasi ex)
        {
            Yukle(Ornek());
            Hata = $"Notebook okunamadı: {ex.Message} Örnek notebook yüklendi.";
            Response.StatusCode = 400;
            return false;
        }
        if (Kaynaklar is not null)
        {
            if (Kaynaklar.Count != Belge.Hucreler.Count)
            {
                Yukle(Belge);
                Hata = "Hücre sayısı formla uyuşmuyor; son kaydedilen hâl gösteriliyor.";
                Response.StatusCode = 400;
                return false;
            }
            for (var i = 0; i < Kaynaklar.Count; i++)
                Belge.Hucreler[i].Kaynak = (Kaynaklar[i] ?? "").Replace("\r\n", "\n");
        }
        return true;
    }

    private IActionResult Tamam(int? odak, string mesaj)
    {
        Yukle(Belge);
        Odak = odak;
        Mesaj = mesaj;
        return Page();
    }

    private IActionResult Uyari(string hata)
    {
        Yukle(Belge);
        Hata = hata;
        return Page();
    }

    private void Yukle(NotebookBelgesi belge)
    {
        Belge = belge;
        BelgeJson = IpynbDonusturucu.Yaz(belge);
        // Model bağlaması form değerlerini tekrar basmasın; sayfa her zaman güncel belgeden çizilir.
        ModelState.Clear();
    }

    private bool Gecerli(int sira) => sira >= 0 && sira < Belge.Hucreler.Count;

    private static NotebookBelgesi Ornek()
    {
        using var akis = typeof(IndexModel).Assembly.GetManifestResourceStream("NotebookOrnegi")!;
        using var okuyucu = new StreamReader(akis);
        return IpynbDonusturucu.Oku(okuyucu.ReadToEnd());
    }

    private static readonly Regex Base64 = new("^[A-Za-z0-9+/]+={0,2}$", RegexOptions.Compiled);

    [GeneratedRegex(@"\x1b\[[0-9;]*[A-Za-z]")]
    private static partial Regex AnsiKodu();
}
