using System.Net;
using System.Text.RegularExpressions;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

// 6.4: Notebook sayfası v1 — hücre listesi, ekle/sil/taşı ve markdown önizleme.
public sealed class NotebookSayfasiTests
{
    [Fact]
    public async Task OrnekNotebookHucreleriVeCiktilariylaGosterilir()
    {
        using var app = new TestApplication();
        var istemci = app.HazirIstemci();
        var html = WebUtility.HtmlDecode(await istemci.GetStringAsync("/Notebook"));

        Assert.Equal(8, Regex.Matches(html, "class=\"notebook-cell ").Count);
        Assert.Contains("<h1>Listeler ve döngüler</h1>", html);
        Assert.Contains("<table>", html);
        Assert.Contains("1\n2\n3\n", html);
        Assert.Contains("<strong>IndexError</strong>: list index out of range", html);
        Assert.Contains("src=\"data:image/png;base64,iVBORw0KGgo", html);
        Assert.DoesNotContain("\u001b[", html, StringComparison.Ordinal);
        Assert.Matches(@"href=""/css/notebook[^""]*\.css", html);
        foreach (var dosya in new[] { "/css/notebook.css", "/js/notebook.js" })
            Assert.Equal(HttpStatusCode.OK, (await istemci.GetAsync(dosya)).StatusCode);
    }

    [Fact]
    public async Task KayitliHtmlCiktisiHamOlarakBasilmaz()
    {
        using var app = new TestApplication();
        var html = await app.HazirIstemci().GetStringAsync("/Notebook");
        // text/html çıktısı yerine text/plain gösterilir; zengin çıktılar 7.4'te ele alınır.
        Assert.DoesNotContain("<b>kalın</b>", html);
        Assert.Contains("&lt;IPython.core.display.HTML object&gt;", html);
    }

    [Fact]
    public async Task HucreEklenirVeDuzenlemelerKorunur()
    {
        using var app = new TestApplication();
        var form = await Form.AcAsync(app);
        form.Kaynaklar[6] = "print(max(sayilar))";

        var sonuc = await form.GonderAsync("Ekle", "sira=7&tur=markdown");
        Assert.Equal(9, sonuc.Belge.Hucreler.Count);
        Assert.Equal(HucreTuru.Markdown, sonuc.Belge.Hucreler[7].Tur);
        Assert.Equal("", sonuc.Belge.Hucreler[7].Kaynak);
        Assert.Equal("print(max(sayilar))", sonuc.Belge.Hucreler[6].Kaynak);
        Assert.Contains("data-odak=\"7\"", sonuc.Html);
    }

    [Fact]
    public async Task HucreSilinirVeSonHucreSilinceBosKodHucresiKalir()
    {
        using var app = new TestApplication();
        var form = await Form.AcAsync(app);
        var sonuc = await form.GonderAsync("Sil", "sira=4");
        Assert.Equal(7, sonuc.Belge.Hucreler.Count);
        Assert.DoesNotContain(sonuc.Belge.Hucreler, h => h.Kimlik == "hata");

        for (var i = sonuc.Belge.Hucreler.Count; i > 0; i--)
            sonuc = await sonuc.GonderAsync("Sil", "sira=0");
        var kalan = Assert.Single(sonuc.Belge.Hucreler);
        Assert.Equal(HucreTuru.Kod, kalan.Tur);
    }

    [Fact]
    public async Task HucreTasinirVeSinirdaTasimaReddedilir()
    {
        using var app = new TestApplication();
        var form = await Form.AcAsync(app);
        var sonuc = await form.GonderAsync("Tasi", "sira=1&yon=1");
        Assert.Equal(["giris", "toplam", "dongu"], sonuc.Belge.Hucreler.Take(3).Select(h => h.Kimlik));

        sonuc = await sonuc.GonderAsync("Tasi", "sira=0&yon=-1");
        Assert.Contains("Hücre bu yönde taşınamaz.", sonuc.Html);
        Assert.Equal("giris", sonuc.Belge.Hucreler[0].Kimlik);
        Assert.Contains("disabled=\"disabled\" aria-label=\"1. hücreyi yukarı taşı\"", sonuc.Html);
    }

    [Fact]
    public async Task MarkdownOnizlemesiGuncellenirVeTemizlenir()
    {
        using var app = new TestApplication();
        var form = await Form.AcAsync(app);
        form.Kaynaklar[5] = "## Yeni başlık\n\n<script>alert(1)</script> [bağlantı](javascript:alert(1))";

        var sonuc = await form.GonderAsync("Onizle", "");
        Assert.Contains("<h2>Yeni başlık</h2>", sonuc.Html);
        // Kullanıcının yazdığı metin textarea içinde kodlanmış olarak durur; önizlemede çalıştırılabilir HTML yoktur.
        Assert.DoesNotContain("<script>", sonuc.HamHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"javascript:", sonuc.HamHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HucreTuruDegisinceCiktilarTemizlenir()
    {
        using var app = new TestApplication();
        var form = await Form.AcAsync(app);
        var sonuc = await form.GonderAsync("Tur", "sira=1");
        Assert.Equal(HucreTuru.Markdown, sonuc.Belge.Hucreler[1].Tur);
        Assert.Empty(sonuc.Belge.Hucreler[1].Ciktilar);

        sonuc = await sonuc.GonderAsync("Tur", "sira=7");
        Assert.Contains("Hücre türü değiştirilemedi.", sonuc.Html);
    }

    [Fact]
    public async Task BozukNotebookFormuOrnegeDonerVe400Verir()
    {
        using var app = new TestApplication();
        var form = await Form.AcAsync(app);
        form.Ipynb = "{\"nbformat\": 3, \"cells\": []}";
        var yanit = await form.HamGonderAsync("Onizle", "");
        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.Contains("Yalnızca nbformat 4 desteklenir", WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task HucreSayisiUyusmazsaDegisiklikUygulanmaz()
    {
        using var app = new TestApplication();
        var form = await Form.AcAsync(app);
        form.Kaynaklar.RemoveAt(0);
        var yanit = await form.HamGonderAsync("Sil", "sira=0");
        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
        Assert.Contains("Hücre sayısı formla uyuşmuyor", WebUtility.HtmlDecode(await yanit.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task AntiforgeryOlmadanIslemYapilmaz()
    {
        using var app = new TestApplication();
        var yanit = await app.HazirIstemci().PostAsync("/Notebook?handler=Sil&sira=0",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["Ipynb"] = "{}" }));
        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
    }

    // Sayfadaki formu bir tarayıcı gibi okuyup geri gönderen küçük yardımcı.
    private sealed class Form
    {
        private readonly HttpClient istemci;
        public string Html { get; }
        public string HamHtml { get; }
        public string Ipynb { get; set; }
        public List<string> Kaynaklar { get; }
        public NotebookBelgesi Belge => IpynbDonusturucu.Oku(Ipynb);
        private readonly string token;

        private Form(HttpClient istemci, string html)
        {
            this.istemci = istemci;
            HamHtml = html;
            Html = WebUtility.HtmlDecode(html);
            token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
            Ipynb = WebUtility.HtmlDecode(Regex.Match(html, "name=\"Ipynb\" value=\"([^\"]*)\"").Groups[1].Value);
            Kaynaklar = Regex.Matches(html, "<textarea[^>]*name=\"Kaynaklar\"[^>]*>(.*?)</textarea>", RegexOptions.Singleline)
                .Select(m => WebUtility.HtmlDecode(m.Groups[1].Value)).Select(s => s.StartsWith('\n') ? s[1..] : s).ToList();
        }

        public static async Task<Form> AcAsync(TestApplication app)
        {
            var istemci = app.HazirIstemci();
            return new Form(istemci, await istemci.GetStringAsync("/Notebook"));
        }

        public async Task<HttpResponseMessage> HamGonderAsync(string isleyici, string sorgu)
        {
            var alanlar = new List<KeyValuePair<string, string>>
            {
                new("__RequestVerificationToken", token), new("Ipynb", Ipynb)
            };
            alanlar.AddRange(Kaynaklar.Select(k => new KeyValuePair<string, string>("Kaynaklar", k)));
            return await istemci.PostAsync($"/Notebook?handler={isleyici}&{sorgu}", new FormUrlEncodedContent(alanlar));
        }

        public async Task<Form> GonderAsync(string isleyici, string sorgu)
        {
            var yanit = await HamGonderAsync(isleyici, sorgu);
            Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
            var sonraki = new Form(istemci, await yanit.Content.ReadAsStringAsync());
            Assert.Equal(sonraki.Belge.Hucreler.Count, sonraki.Kaynaklar.Count);
            return sonraki;
        }
    }
}
