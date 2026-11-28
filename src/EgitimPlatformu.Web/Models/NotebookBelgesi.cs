using System.Text.Json.Nodes;

namespace EgitimPlatformu.Web.Models;

// .ipynb (nbformat 4) dosyasının bellekteki karşılığı. Veritabanı varlığı değildir;
// okuma ve yazma IpynbDonusturucu ile yapılır.
public sealed class NotebookBelgesi
{
    public int Nbformat { get; set; } = 4;
    public int NbformatMinor { get; set; } = 5;
    public JsonObject Metadata { get; set; } = [];
    public List<NotebookHucresi> Hucreler { get; set; } = [];

    // Modelde karşılığı olmayan üst düzey alanlar yazarken aynen geri konur.
    public JsonObject EkAlanlar { get; set; } = [];

    public static NotebookBelgesi YeniPython() => new()
    {
        Metadata = new JsonObject
        {
            ["kernelspec"] = new JsonObject
            {
                ["display_name"] = "Python 3", ["language"] = "python", ["name"] = "python3"
            },
            ["language_info"] = new JsonObject { ["name"] = "python" }
        },
        Hucreler = [NotebookHucresi.Yeni(HucreTuru.Kod)]
    };

    public string CekirdekAdi =>
        Metadata["kernelspec"]?["name"]?.GetValue<string>() ?? "python3";
}

public enum HucreTuru { Markdown, Kod, Ham }

public sealed class NotebookHucresi
{
    public string? Kimlik { get; set; }
    public HucreTuru Tur { get; set; }
    public string Kaynak { get; set; } = "";
    public int? CalistirmaSayaci { get; set; }
    public JsonObject Metadata { get; set; } = [];
    public List<NotebookCiktisi> Ciktilar { get; set; } = [];

    // Ek dosyalar (attachments) gibi modelde olmayan hücre alanları.
    public JsonObject EkAlanlar { get; set; } = [];

    public static NotebookHucresi Yeni(HucreTuru tur, string kaynak = "") => new()
    {
        Kimlik = Guid.NewGuid().ToString("N")[..8],
        Tur = tur,
        Kaynak = kaynak
    };
}

public enum CiktiTuru { Akis, CalistirmaSonucu, Gosterim, Hata }

public sealed class NotebookCiktisi
{
    public CiktiTuru Tur { get; set; }

    // Akış çıktısı: stdout veya stderr.
    public string? AkisAdi { get; set; }
    public string? Metin { get; set; }

    // execute_result ve display_data: MIME türüne göre içerik. Metin türleri
    // birleştirilmiş tek string olarak, application/json gibi yapılar JSON olarak tutulur.
    public Dictionary<string, JsonNode?> Veri { get; set; } = [];
    public JsonObject Metadata { get; set; } = [];
    public int? CalistirmaSayaci { get; set; }

    // Hata çıktısı.
    public string? HataAdi { get; set; }
    public string? HataDegeri { get; set; }
    public List<string> Izleme { get; set; } = [];

    public JsonObject EkAlanlar { get; set; } = [];

    public string? MetinVerisi(string mime) =>
        Veri.TryGetValue(mime, out var deger) && deger is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}
