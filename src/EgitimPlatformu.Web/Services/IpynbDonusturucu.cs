using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using EgitimPlatformu.Web.Models;

namespace EgitimPlatformu.Web.Services;

public sealed class NotebookBicimHatasi(string mesaj) : Exception(mesaj);

// nbformat 4 .ipynb dosyalarını NotebookBelgesi modeline okur ve geri yazar.
// Yazım Jupyter'in kendi biçimini izler: alfabetik anahtarlar, 1 boşluk girinti,
// çok satırlı metinler satır dizisi olarak. Böylece Jupyter'de kaydedilmiş bir
// dosya okunup yazıldığında aynı içerik elde edilir.
public static partial class IpynbDonusturucu
{
    public const int AzamiBoyut = 4 * 1024 * 1024;

    private static readonly JsonSerializerOptions YazmaAyari = new()
    {
        WriteIndented = true,
        IndentSize = 1,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string[] HucreAlanlari = ["cell_type", "execution_count", "id", "metadata", "outputs", "source"];
    private static readonly string[] BelgeAlanlari = ["cells", "metadata", "nbformat", "nbformat_minor"];

    public static NotebookBelgesi Oku(string json)
    {
        if (json.Length > AzamiBoyut) throw new NotebookBicimHatasi("Notebook dosyası 4 MB sınırını aşıyor.");
        JsonNode? kok;
        try
        {
            kok = JsonNode.Parse(json, documentOptions: new JsonDocumentOptions { MaxDepth = 64 });
        }
        catch (JsonException)
        {
            throw new NotebookBicimHatasi("Dosya geçerli bir JSON değil.");
        }
        if (kok is not JsonObject belge) throw new NotebookBicimHatasi("Notebook kökü bir JSON nesnesi olmalı.");

        var surum = Tamsayi(belge["nbformat"], "nbformat");
        if (surum != 4) throw new NotebookBicimHatasi($"Yalnızca nbformat 4 desteklenir (dosya: {surum}).");
        if (belge["cells"] is not JsonArray hucreler) throw new NotebookBicimHatasi("'cells' alanı bir dizi olmalı.");

        return new NotebookBelgesi
        {
            Nbformat = surum,
            NbformatMinor = belge["nbformat_minor"] is null ? 0 : Tamsayi(belge["nbformat_minor"], "nbformat_minor"),
            Metadata = Nesne(belge["metadata"]),
            Hucreler = hucreler.Select((h, i) => HucreOku(h, i)).ToList(),
            EkAlanlar = Kalanlar(belge, BelgeAlanlari)
        };
    }

    public static string Yaz(NotebookBelgesi notebook)
    {
        var belge = new JsonObject
        {
            ["cells"] = new JsonArray(notebook.Hucreler.Select(HucreYaz).ToArray<JsonNode?>()),
            ["metadata"] = Kopya(notebook.Metadata),
            ["nbformat"] = notebook.Nbformat,
            ["nbformat_minor"] = notebook.NbformatMinor
        };
        Ekle(belge, notebook.EkAlanlar);
        var json = Sirala(belge)!.ToJsonString(YazmaAyari);
        // Python kaçışları küçük harf yazar (\u001b); .NET büyük harf üretir.
        return KacisDuzeni().Replace(json, m => m.Groups[1].Value + @"\u" + m.Groups[2].Value.ToLowerInvariant()) + "\n";
    }

    // Kod çalıştırma servisinin döndürdüğü (Jupyter mesajından üretilmiş) çıktıyı modele çevirir.
    public static NotebookCiktisi CiktiOku(JsonElement cikti) =>
        CiktiOku(JsonNode.Parse(cikti.GetRawText()), 0, 0);

    private static NotebookHucresi HucreOku(JsonNode? dugum, int sira)
    {
        if (dugum is not JsonObject h) throw new NotebookBicimHatasi($"{sira + 1}. hücre bir nesne değil.");
        var tur = Metin(h["cell_type"]) switch
        {
            "markdown" => HucreTuru.Markdown,
            "code" => HucreTuru.Kod,
            "raw" => HucreTuru.Ham,
            var t => throw new NotebookBicimHatasi($"{sira + 1}. hücrenin türü bilinmiyor: '{t}'.")
        };
        var hucre = new NotebookHucresi
        {
            Kimlik = Metin(h["id"]),
            Tur = tur,
            Kaynak = SatirlariBirlestir(h["source"], $"{sira + 1}. hücrenin kaynağı"),
            Metadata = Nesne(h["metadata"]),
            EkAlanlar = Kalanlar(h, HucreAlanlari)
        };
        if (tur == HucreTuru.Kod)
        {
            hucre.CalistirmaSayaci = h["execution_count"] is JsonValue v && v.TryGetValue<int>(out var n) ? n : null;
            if (h["outputs"] is JsonArray ciktilar)
                hucre.Ciktilar = ciktilar.Select((c, j) => CiktiOku(c, sira, j)).ToList();
        }
        return hucre;
    }

    private static NotebookCiktisi CiktiOku(JsonNode? dugum, int hucre, int sira)
    {
        var yer = $"{hucre + 1}. hücrenin {sira + 1}. çıktısı";
        if (dugum is not JsonObject c) throw new NotebookBicimHatasi($"{yer} bir nesne değil.");
        var cikti = new NotebookCiktisi { Metadata = Nesne(c["metadata"]) };
        string[] bilinen;
        switch (Metin(c["output_type"]))
        {
            case "stream":
                cikti.Tur = CiktiTuru.Akis;
                cikti.AkisAdi = Metin(c["name"]) ?? "stdout";
                cikti.Metin = SatirlariBirlestir(c["text"], yer);
                bilinen = ["output_type", "name", "text"];
                break;
            case "execute_result":
            case "display_data":
                cikti.Tur = Metin(c["output_type"]) == "display_data" ? CiktiTuru.Gosterim : CiktiTuru.CalistirmaSonucu;
                cikti.CalistirmaSayaci = c["execution_count"] is JsonValue v && v.TryGetValue<int>(out var n) ? n : null;
                foreach (var (mime, deger) in Nesne(c["data"]))
                    cikti.Veri[mime] = MetinMime(mime) ? JsonValue.Create(SatirlariBirlestir(deger, yer)) : deger?.DeepClone();
                bilinen = ["output_type", "execution_count", "data", "metadata"];
                break;
            case "error":
                cikti.Tur = CiktiTuru.Hata;
                cikti.HataAdi = Metin(c["ename"]) ?? "";
                cikti.HataDegeri = Metin(c["evalue"]) ?? "";
                cikti.Izleme = c["traceback"] is JsonArray iz ? iz.Select(s => Metin(s) ?? "").ToList() : [];
                bilinen = ["output_type", "ename", "evalue", "traceback"];
                break;
            case var t:
                throw new NotebookBicimHatasi($"{yer} türü bilinmiyor: '{t}'.");
        }
        cikti.EkAlanlar = Kalanlar(c, [.. bilinen, "metadata"]);
        return cikti;
    }

    private static JsonObject HucreYaz(NotebookHucresi hucre)
    {
        var h = new JsonObject
        {
            ["cell_type"] = hucre.Tur switch
            {
                HucreTuru.Markdown => "markdown", HucreTuru.Kod => "code", _ => "raw"
            }
        };
        if (hucre.Tur == HucreTuru.Kod) h["execution_count"] = hucre.CalistirmaSayaci;
        if (hucre.Kimlik is not null) h["id"] = hucre.Kimlik;
        h["metadata"] = Kopya(hucre.Metadata);
        if (hucre.Tur == HucreTuru.Kod)
            h["outputs"] = new JsonArray(hucre.Ciktilar.Select(CiktiYaz).ToArray<JsonNode?>());
        h["source"] = SatirlaraBol(hucre.Kaynak);
        Ekle(h, hucre.EkAlanlar);
        return h;
    }

    private static JsonObject CiktiYaz(NotebookCiktisi cikti)
    {
        var c = new JsonObject();
        switch (cikti.Tur)
        {
            case CiktiTuru.Akis:
                c["name"] = cikti.AkisAdi ?? "stdout";
                c["output_type"] = "stream";
                c["text"] = SatirlaraBol(cikti.Metin ?? "");
                break;
            case CiktiTuru.CalistirmaSonucu:
            case CiktiTuru.Gosterim:
                var veri = new JsonObject();
                foreach (var (mime, deger) in cikti.Veri)
                    veri[mime] = MetinMime(mime) && deger is JsonValue v && v.TryGetValue<string>(out var s)
                        ? SatirlaraBol(s) : deger?.DeepClone();
                c["data"] = veri;
                if (cikti.Tur == CiktiTuru.CalistirmaSonucu) c["execution_count"] = cikti.CalistirmaSayaci;
                c["metadata"] = Kopya(cikti.Metadata);
                c["output_type"] = cikti.Tur == CiktiTuru.Gosterim ? "display_data" : "execute_result";
                break;
            case CiktiTuru.Hata:
                c["ename"] = cikti.HataAdi ?? "";
                c["evalue"] = cikti.HataDegeri ?? "";
                c["output_type"] = "error";
                c["traceback"] = new JsonArray(cikti.Izleme.Select(s => (JsonNode?)s).ToArray());
                break;
        }
        Ekle(c, cikti.EkAlanlar);
        return c;
    }

    // nbformat'ın kuralı: text/* ile JavaScript ve SVG satır dizisi olarak saklanır,
    // görseller (image/png) tek base64 string olarak kalır.
    private static bool MetinMime(string mime) =>
        mime.StartsWith("text/", StringComparison.Ordinal) || mime is "application/javascript" or "image/svg+xml";

    private static string SatirlariBirlestir(JsonNode? dugum, string yer) => dugum switch
    {
        null => "",
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        JsonArray dizi => string.Concat(dizi.Select(p => Metin(p)
            ?? throw new NotebookBicimHatasi($"{yer} yalnızca metin içermeli."))),
        _ => throw new NotebookBicimHatasi($"{yer} metin ya da metin dizisi olmalı.")
    };

    // "a\nb" -> ["a\n", "b"]; Jupyter de satır sonlarını satırın içinde tutar.
    private static JsonArray SatirlaraBol(string metin)
    {
        var dizi = new JsonArray();
        var bas = 0;
        for (var i = 0; i < metin.Length; i++)
        {
            if (metin[i] != '\n') continue;
            dizi.Add(metin[bas..(i + 1)]);
            bas = i + 1;
        }
        if (bas < metin.Length) dizi.Add(metin[bas..]);
        return dizi;
    }

    private static string? Metin(JsonNode? dugum) =>
        dugum is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static int Tamsayi(JsonNode? dugum, string alan) =>
        dugum is JsonValue v && v.TryGetValue<int>(out var n) ? n
            : throw new NotebookBicimHatasi($"'{alan}' alanı bir tamsayı olmalı.");

    private static JsonObject Nesne(JsonNode? dugum) =>
        dugum is JsonObject o ? (JsonObject)o.DeepClone() : [];

    private static JsonObject Kopya(JsonObject nesne) => (JsonObject)nesne.DeepClone();

    private static JsonObject Kalanlar(JsonObject kaynak, string[] bilinen)
    {
        var ek = new JsonObject();
        foreach (var (ad, deger) in kaynak)
            if (!bilinen.Contains(ad)) ek[ad] = deger?.DeepClone();
        return ek;
    }

    private static void Ekle(JsonObject hedef, JsonObject ek)
    {
        foreach (var (ad, deger) in ek)
            if (!hedef.ContainsKey(ad)) hedef[ad] = deger?.DeepClone();
    }

    // Jupyter anahtarları alfabetik yazar (sort_keys=True); aynı sırayı her düzeyde uygularız.
    private static JsonNode? Sirala(JsonNode? dugum) => dugum switch
    {
        JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Sirala(p.Value?.DeepClone())))),
        JsonArray a => new JsonArray(a.Select(x => Sirala(x?.DeepClone())).ToArray()),
        _ => dugum?.DeepClone()
    };

    // Çift sayıda ters bölü ardından gelen \uXXXX gerçek bir kaçıştır.
    [GeneratedRegex(@"(?<!\\)((?:\\\\)*)\\u([0-9A-F]{4})")]
    private static partial Regex KacisDuzeni();
}
