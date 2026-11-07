using System.Text.RegularExpressions;

namespace EgitimPlatformu.Web.Services;

public sealed record GorevDurumu(int No, string Durum);

public sealed record KodCalistirYaniti(bool Basarili, string Durum, string Cikti, string Hata,
    IReadOnlyList<GorevDurumu> Gorevler);

// pytest -v satırlarını (dosya.py::test_gorev_N[...] PASSED) görev numarasına göre toplar.
// Durum adları mobil sözleşmesiyle aynıdır: basarili, basarisiz, atlanmis, test_yok.
public static partial class PytestGorevAyristirici
{
    [GeneratedRegex(@"^[^\s:]+::test_gorev_(\d+)(?!\d)[^\n]*?\s(PASSED|FAILED|ERROR|SKIPPED|XFAIL|XPASS)(?=\s|$)",
        RegexOptions.Multiline)]
    private static partial Regex TestSatiri();

    public static KodCalistirYaniti Ayristir(KodCalistirmaSonucu sonuc, IEnumerable<int> gorevNumaralari)
    {
        var durumlar = new Dictionary<int, string>();
        foreach (Match eslesme in TestSatiri().Matches(sonuc.StandartCikti))
        {
            var no = int.Parse(eslesme.Groups[1].Value);
            var yeni = eslesme.Groups[2].Value switch
            {
                "PASSED" or "XPASS" => "basarili",
                "SKIPPED" or "XFAIL" => "atlanmis",
                _ => "basarisiz"
            };
            // Aynı görevin birden çok testi varsa en kötü sonuç kalır.
            if (!durumlar.TryGetValue(no, out var eski) || Oncelik(yeni) > Oncelik(eski)) durumlar[no] = yeni;
        }

        var gorevler = gorevNumaralari.Distinct().Order()
            .Select(no => new GorevDurumu(no, durumlar.GetValueOrDefault(no, "test_yok")))
            .ToArray();
        var durum = sonuc.CikisKodu switch
        {
            0 => "tamamlandi",
            1 => "basarisiz",
            5 => "test_yok",
            124 => "zaman_asimi",
            _ => "hata"
        };
        return new(sonuc.Basarili, durum, sonuc.StandartCikti, sonuc.StandartHata, gorevler);
    }

    private static int Oncelik(string durum) => durum switch
    {
        "basarisiz" => 2,
        "atlanmis" => 1,
        _ => 0
    };
}
