using System.Text.Json;
using System.Text.Json.Nodes;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

// 6.3: .ipynb dosyasının C# modeline okunması ve geri yazılması.
public sealed class IpynbDonusturucuTests
{
    // nbformat 5.11 ile üretilmiş, doğrulanmış örnek dosya.
    private static string Ornek() => File.ReadAllText(Yol("notebook-ornegi.ipynb"));

    [Fact]
    public void JupyterDosyasiOkunupYazilincaBaytBaytAyniKalir()
    {
        var ham = Ornek();
        Assert.Equal(ham, IpynbDonusturucu.Yaz(IpynbDonusturucu.Oku(ham)));
    }

    [Fact]
    public void HucreTurleriKaynakVeKimliklerOkunur()
    {
        var nb = IpynbDonusturucu.Oku(Ornek());
        Assert.Equal(4, nb.Nbformat);
        Assert.Equal("python3", nb.CekirdekAdi);
        Assert.Equal([HucreTuru.Markdown, HucreTuru.Kod, HucreTuru.Kod, HucreTuru.Kod, HucreTuru.Kod,
            HucreTuru.Markdown, HucreTuru.Kod, HucreTuru.Ham], nb.Hucreler.Select(h => h.Tur));
        Assert.Equal("giris", nb.Hucreler[0].Kimlik);
        Assert.StartsWith("# Listeler ve döngüler\n\n", nb.Hucreler[0].Kaynak);
        Assert.Equal("sayilar = [3, 1, 2]\nfor s in sorted(sayilar):\n    print(s)", nb.Hucreler[1].Kaynak);
        Assert.Equal(1, nb.Hucreler[1].CalistirmaSayaci);
        Assert.Null(nb.Hucreler[6].CalistirmaSayaci);
        Assert.Empty(nb.Hucreler[6].Ciktilar);
        Assert.Equal("text/plain", nb.Hucreler[7].Metadata["raw_mimetype"]!.GetValue<string>());
    }

    [Fact]
    public void DortCiktiTuruOkunur()
    {
        var nb = IpynbDonusturucu.Oku(Ornek());
        var akis = Assert.Single(nb.Hucreler[1].Ciktilar);
        Assert.Equal(CiktiTuru.Akis, akis.Tur);
        Assert.Equal("stdout", akis.AkisAdi);
        Assert.Equal("1\n2\n3\n", akis.Metin);

        var sonuc = Assert.Single(nb.Hucreler[2].Ciktilar);
        Assert.Equal(CiktiTuru.CalistirmaSonucu, sonuc.Tur);
        Assert.Equal(2, sonuc.CalistirmaSayaci);
        Assert.Equal("6", sonuc.MetinVerisi("text/plain"));

        var gosterimler = nb.Hucreler[3].Ciktilar;
        Assert.All(gosterimler, g => Assert.Equal(CiktiTuru.Gosterim, g.Tur));
        Assert.Equal("<b>kalın</b>\n<i>eğik</i>", gosterimler[0].MetinVerisi("text/html"));
        Assert.StartsWith("iVBORw0KGgo", gosterimler[1].MetinVerisi("image/png"));
        Assert.Equal(1, gosterimler[1].Metadata["image/png"]!["width"]!.GetValue<int>());

        var hata = Assert.Single(nb.Hucreler[4].Ciktilar);
        Assert.Equal(CiktiTuru.Hata, hata.Tur);
        Assert.Equal("IndexError", hata.HataAdi);
        Assert.Equal("list index out of range", hata.HataDegeri);
        Assert.Single(hata.Izleme);
    }

    [Fact]
    public void TekStringKaynakKabulEdilirVeSatirDizisiOlarakYazilir()
    {
        var nb = IpynbDonusturucu.Oku("""
            {"nbformat": 4, "nbformat_minor": 4, "metadata": {},
             "cells": [{"cell_type": "code", "metadata": {}, "execution_count": null,
                        "outputs": [], "source": "a = 1\nb = 2\n"}]}
            """);
        Assert.Equal("a = 1\nb = 2\n", nb.Hucreler[0].Kaynak);
        Assert.Null(nb.Hucreler[0].Kimlik);

        var yazilan = JsonNode.Parse(IpynbDonusturucu.Yaz(nb))!;
        Assert.Equal("""["a = 1\n","b = 2\n"]""", yazilan["cells"]![0]!["source"]!.ToJsonString());
        Assert.False(yazilan["cells"]![0]!.AsObject().ContainsKey("id"));
    }

    [Fact]
    public void KacisKarakterleriVeTersBoluMetniBozulmaz()
    {
        var nb = NotebookBelgesi.YeniPython();
        nb.Hucreler[0].Kaynak = "print('\\u001B yazısı')\x1b\"";
        var yazilan = IpynbDonusturucu.Yaz(nb);
        Assert.Contains(@"\\u001B yazısı", yazilan);
        Assert.Contains(@"\u001b\""", yazilan);
        Assert.Equal(nb.Hucreler[0].Kaynak, IpynbDonusturucu.Oku(yazilan).Hucreler[0].Kaynak);
    }

    [Fact]
    public void BilinmeyenAlanlarVeEklerKorunur()
    {
        var nb = IpynbDonusturucu.Oku("""
            {"nbformat": 4, "nbformat_minor": 5, "metadata": {"colab": {"provenance": []}},
             "ozel_alan": {"x": 1},
             "cells": [{"cell_type": "markdown", "id": "a1", "metadata": {"tags": ["ozet"]},
                        "attachments": {"r.png": {"image/png": "AAAA"}}, "source": ["![r](attachment:r.png)"]}]}
            """);
        var yazilan = JsonNode.Parse(IpynbDonusturucu.Yaz(nb))!;
        Assert.Equal(1, yazilan["ozel_alan"]!["x"]!.GetValue<int>());
        Assert.NotNull(yazilan["metadata"]!["colab"]);
        Assert.Equal("AAAA", yazilan["cells"]![0]!["attachments"]!["r.png"]!["image/png"]!.GetValue<string>());
        Assert.Equal("ozet", yazilan["cells"]![0]!["metadata"]!["tags"]![0]!.GetValue<string>());
    }

    [Fact]
    public void ModeldeYapilanDegisiklikDosyayaYansir()
    {
        var nb = IpynbDonusturucu.Oku(Ornek());
        nb.Hucreler.RemoveAt(4);
        nb.Hucreler.Insert(0, NotebookHucresi.Yeni(HucreTuru.Markdown, "Yeni başlık"));
        nb.Hucreler[^2].Kaynak = "print(max(sayilar))";

        var tekrar = IpynbDonusturucu.Oku(IpynbDonusturucu.Yaz(nb));
        Assert.Equal(8, tekrar.Hucreler.Count);
        Assert.Equal("Yeni başlık", tekrar.Hucreler[0].Kaynak);
        Assert.Equal(8, tekrar.Hucreler[0].Kimlik!.Length);
        Assert.DoesNotContain(tekrar.Hucreler, h => h.Kimlik == "hata");
        Assert.Equal("print(max(sayilar))", tekrar.Hucreler[^2].Kaynak);
    }

    [Fact]
    public void YeniPythonNotebookuGecerliDosyaUretir()
    {
        var yazilan = IpynbDonusturucu.Yaz(NotebookBelgesi.YeniPython());
        var tekrar = IpynbDonusturucu.Oku(yazilan);
        Assert.Equal("python3", tekrar.CekirdekAdi);
        var hucre = Assert.Single(tekrar.Hucreler);
        Assert.Equal(HucreTuru.Kod, hucre.Tur);
        Assert.Contains("\"execution_count\": null", yazilan);
        Assert.EndsWith("}\n", yazilan);
    }

    [Fact]
    public void SandboxCiktisiModeleCevrilir()
    {
        using var json = JsonDocument.Parse("""
            [{"output_type": "stream", "name": "stderr", "text": "uyarı\n"},
             {"output_type": "execute_result", "execution_count": 3, "data": {"text/plain": "42"}, "metadata": {}},
             {"output_type": "error", "ename": "ValueError", "evalue": "deneme", "traceback": ["satır"]}]
            """);
        var ciktilar = json.RootElement.EnumerateArray().Select(IpynbDonusturucu.CiktiOku).ToList();
        Assert.Equal("stderr", ciktilar[0].AkisAdi);
        Assert.Equal("42", ciktilar[1].MetinVerisi("text/plain"));
        Assert.Equal("ValueError", ciktilar[2].HataAdi);
    }

    [Theory]
    [InlineData("bozuk", "geçerli bir JSON")]
    [InlineData("[]", "JSON nesnesi")]
    [InlineData("""{"nbformat": 3, "cells": []}""", "nbformat 4")]
    [InlineData("""{"nbformat": 4}""", "'cells'")]
    [InlineData("""{"nbformat": 4, "cells": [{"cell_type": "resim", "source": ""}]}""", "türü bilinmiyor")]
    [InlineData("""{"nbformat": 4, "cells": [{"cell_type": "code", "source": [1, 2]}]}""", "yalnızca metin")]
    [InlineData("""{"nbformat": 4, "cells": [{"cell_type": "code", "source": "", "outputs": [{"output_type": "garip"}]}]}""", "1. hücrenin 1. çıktısı")]
    public void GecersizDosyaAnlasilirHataVerir(string json, string beklenen)
    {
        var hata = Assert.Throws<NotebookBicimHatasi>(() => IpynbDonusturucu.Oku(json));
        Assert.Contains(beklenen, hata.Message);
    }

    [Fact]
    public void BoyutSiniriAsilincaReddedilir()
    {
        var buyuk = new string(' ', IpynbDonusturucu.AzamiBoyut + 1);
        Assert.Throws<NotebookBicimHatasi>(() => IpynbDonusturucu.Oku(buyuk));
    }

    internal static string Yol(string ad)
    {
        var dizin = new DirectoryInfo(AppContext.BaseDirectory);
        while (dizin is not null && !File.Exists(Path.Combine(dizin.FullName, "EgitimPlatformu.slnx"))) dizin = dizin.Parent;
        return Path.Combine(dizin!.FullName, "docs", "ornekler", ad);
    }
}
