using System.Text.Json;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Models;
namespace EgitimPlatformu.Web.Data;

public static class GuncelPythonOrnegi
{
    public static void Uygula(Egitim[] dersler, Gorev[] gorevler)
    {
        using var stream = typeof(GuncelPythonOrnegi).Assembly.GetManifestResourceStream("PythonOrnegi")!;
        var paket = JsonSerializer.Deserialize<IcerikPaketi>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var kaynak = paket.Kategoriler[0].Egitimler[0];
        var ders = dersler.Single(e => e.Kod == "PY-01");
        ders.Baslik = kaynak.Baslik; ders.IcerikMarkdown = kaynak.IcerikMarkdown;
        ders.BaslangicKodu = kaynak.BaslangicKodu; ders.TestKodu = kaynak.TestKodu;
        foreach (var gorev in gorevler.Where(g => g.EgitimId == ders.Id))
        {
            var g = kaynak.Gorevler.SingleOrDefault(g => g.No == gorev.No);
            gorev.Aktif = g is not null;
            if (g is null) continue;
            gorev.Tur = g.Tur!; gorev.Baslik = g.Baslik; gorev.Aciklama = g.Aciklama;
            gorev.BaslangicDosyasi = g.BaslangicDosyasi; gorev.BaslangicKodu = g.BaslangicKodu!;
            gorev.KontrolJson = g.Kontrol is null ? null : JsonSerializer.Serialize(g.Kontrol);
        }
    }
}
