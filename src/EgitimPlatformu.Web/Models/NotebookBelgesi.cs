using System.Text.Json;
using System.Text.Json.Nodes;

namespace EgitimPlatformu.Web.Models;

public sealed class NotebookBelgesi
{
    private readonly JsonObject belge;
    private JsonArray Hucreler => (JsonArray)belge["cells"]!;
    private NotebookBelgesi(JsonObject belge) => this.belge = belge;

    public static NotebookBelgesi Oku(string json)
    {
        var belge = JsonNode.Parse(json) as JsonObject;
        if (belge?["nbformat"] is not JsonValue ana || !ana.TryGetValue<int>(out var surum) || surum != 4 ||
            belge["nbformat_minor"] is not JsonValue alt || !alt.TryGetValue<int>(out var minor) || minor < 0 ||
            belge["metadata"] is not JsonObject || belge["cells"] is not JsonArray hucreler ||
            hucreler.Any(h => h is not JsonObject))
            throw new JsonException("Geçerli bir nbformat 4 notebook belgesi gönderin.");
        // JsonObject ayrıştırmayı ilk erişime erteler. Düzenleyicinin kullandığı hücreleri
        // burada açarak yinelenen alanlı bir belgenin kaydedilmesini önle.
        foreach (var hucre in hucreler.OfType<JsonObject>()) _ = hucre.Count;
        return new(belge);
    }

    public static bool Gecerli(JsonElement belge)
    {
        if (belge.ValueKind != JsonValueKind.Object) return false;
        try { Oku(belge.GetRawText()); return true; }
        catch (Exception ex) when (ex is JsonException or ArgumentException) { return false; }
    }

    public static NotebookBelgesi Bos() => Oku("""{"nbformat":4,"nbformat_minor":5,"metadata":{},"cells":[]}""");
    public string Yaz() => belge.ToJsonString();
    public IReadOnlyList<NotebookHucresi> OkuHucreler() => Hucreler.Select((h, i) =>
    {
        var hucre = (JsonObject)h!;
        var tur = hucre["cell_type"] is JsonValue k && k.TryGetValue<string>(out var ad) ? ad : "";
        return new NotebookHucresi(i, tur, KaynakMetni(hucre["source"]), hucre["outputs"]?.ToJsonString());
    }).ToList();

    private static string KaynakMetni(JsonNode? kaynak) => kaynak switch
    {
            JsonValue v when v.TryGetValue<string>(out var metin) => metin,
            JsonArray dizi => string.Concat(dizi.OfType<JsonValue>()
                .Select(v => v.TryGetValue<string>(out var satir) ? satir : "")),
            _ => ""
    };

    public bool Degistir(string islem, int sira, string? kaynak)
    {
        if (islem is "kod" or "markdown")
        {
            var hucre = new JsonObject
            {
                ["cell_type"] = islem == "kod" ? "code" : islem,
                ["metadata"] = new JsonObject(), ["source"] = ""
            };
            if (belge["nbformat_minor"]!.GetValue<int>() >= 5) hucre["id"] = Guid.NewGuid().ToString("N");
            if (islem == "kod") { hucre["outputs"] = new JsonArray(); hucre["execution_count"] = null; }
            Hucreler.Add(hucre);
            return true;
        }
        if (sira < 0 || sira >= Hucreler.Count) return false;
        var secili = (JsonObject)Hucreler[sira]!;
        if (islem == "kaydet")
        {
            var yeni = (kaynak ?? "").Replace("\r\n", "\n");
            if (KaynakMetni(secili["source"]).Replace("\r\n", "\n") != yeni) secili["source"] = yeni;
            return true;
        }
        if (islem == "sil") { Hucreler.RemoveAt(sira); return true; }
        var hedef = islem switch { "yukari" => sira - 1, "asagi" => sira + 1, _ => -1 };
        if (hedef < 0 || hedef >= Hucreler.Count) return false;
        Hucreler.RemoveAt(sira);
        Hucreler.Insert(hedef, secili);
        return true;
    }
}

public sealed record NotebookHucresi(int Sira, string Tur, string Kaynak, string? Ciktilar);
