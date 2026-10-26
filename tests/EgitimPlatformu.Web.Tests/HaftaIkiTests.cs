using System.Net;
using System.Text.RegularExpressions;
using EgitimPlatformu.Web.Services;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

public sealed class HaftaIkiTests : IClassFixture<TestApplication>, IDisposable
{
    private readonly TestApplication app;
    private readonly HttpClient client;

    public HaftaIkiTests(TestApplication app)
    {
        this.app = app;
        app.Hesap.Kullanilabilir = false;
        app.Hesap.Basarili = true;
        app.Hesap.CikisCagrilari = 0;
        app.VeritabaniHatasiUret = false;
        client = app.HazirIstemci();
    }

    [Theory]
    [InlineData("/Egitimler", 3)]
    [InlineData("/Egitimler?KategoriId=1", 3)]
    [InlineData("/Egitimler?KategoriId=2", 0)]
    [InlineData("/Egitimler?Q=PY-03", 1)]
    [InlineData("/Egitimler?KategoriId=2&Q=PY-03", 0)]
    [InlineData("/Egitimler?Q=%27%20OR%201%3D1--", 0)]
    public async Task KatalogGercekVeriyiFiltreler(string url, int expected)
    {
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(expected, Regex.Matches(html, "class=\"course-card ").Count);
        if (expected == 0) Assert.Contains("eşleşen", WebUtility.HtmlDecode(html));
    }

    [Theory]
    [InlineData("/Egitimler?KategoriId=abc", HttpStatusCode.BadRequest)]
    [InlineData("/Egitimler?KategoriId=-1", HttpStatusCode.BadRequest)]
    [InlineData("/Egitimler?KategoriId=999", HttpStatusCode.NotFound)]
    [InlineData("/Egitimler/Detay/abc", HttpStatusCode.NotFound)]
    [InlineData("/Egitimler/Detay/0", HttpStatusCode.NotFound)]
    [InlineData("/Egitimler/Detay/999", HttpStatusCode.NotFound)]
    [InlineData("/Egitimler/Detay", HttpStatusCode.NotFound)]
    public async Task GecersizFiltreVeDerslerEleAlinir(string url, HttpStatusCode expected)
        => Assert.Equal(expected, (await client.GetAsync(url)).StatusCode);

    [Fact]
    public async Task DetayMarkdownGorevVeBaslangicKodunuGosterir()
    {
        var response = await client.GetAsync("/Egitimler/Detay/1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(8, Regex.Matches(html, "class=\"task-item\"").Count);
        Assert.Contains("<strong>Kabul", html);
        Assert.Contains("dakika_saniye", html);
        Assert.Contains("kod çalıştırılmaz", WebUtility.HtmlDecode(html));
        Assert.DoesNotContain("@pytest", html); // TestKodu öğrenci sayfasında yayımlanmaz.
    }

    [Fact]
    public async Task AnaSayfaGercekDerslereYonlendirir()
    {
        var html = await client.GetStringAsync("/");
        Assert.Equal(3, Regex.Matches(html, "class=\"course-card ").Count);
        Assert.Contains("href=\"/Egitimler/Detay/1\"", html);
        Assert.Contains("href=\"/Hesap/Giris\"", html);
        Assert.DoesNotContain("Sıfırdan Python Programlama", html);
    }

    [Theory]
    [InlineData("Giris")]
    [InlineData("Kaydol")]
    public async Task EksikIdentityAcikcaBelirtilir(string page)
    {
        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Hesap/{page}"));
        Assert.Contains("henüz kullanıma açılmadı", html);
        Assert.Contains("disabled", html);
        Assert.Contains("jquery.validate.unobtrusive.min.", html);
        Assert.Contains("data-val-required", html);
    }

    [Fact]
    public async Task DogrulamaVeStilDosyalariGercektenSunulur()
    {
        var html = await client.GetStringAsync("/Hesap/Kaydol");
        var assets = Regex.Matches(html, "(?:href|src)=\"([^\"]+(?:jquery[^\"]*|site[^\"]*|bootstrap[^\"]*)\\.(?:js|css))\"")
            .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)).ToList();
        Assert.NotEmpty(assets);
        foreach (var asset in assets) Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(asset)).StatusCode);
    }

    [Fact]
    public async Task KayitSunucudaDogrulanirVeSifreGeriYazilmaz()
    {
        var response = await PostForm("/Hesap/Kaydol", new()
        {
            ["Input.Eposta"] = "hatali",
            ["Input.Sifre"] = "kisa",
            ["Input.SifreTekrar"] = "farkli"
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Geçerli bir e-posta", html);
        Assert.Contains("8–128", html);
        Assert.Contains("Şifreler eşleşmiyor", html);
        Assert.DoesNotContain("value=\"kisa\"", html);
    }

    [Fact]
    public async Task EksikAltyapiGecerliKaydiBasariliGostermez()
    {
        var response = await PostForm("/Hesap/Kaydol", new()
        {
            ["Input.Eposta"] = "nisa@example.test", ["Input.Sifre"] = "Guvenli123!", ["Input.SifreTekrar"] = "Guvenli123!"
        });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task GirisZorunluAlanlariSunucudaDogrular()
    {
        var response = await PostForm("/Hesap/Giris", new());
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("E-posta adresinizi girin", html);
        Assert.Contains("Şifrenizi girin", html);
    }

    [Fact]
    public async Task BasarisizGirisAnlasilirMesajGosterir()
    {
        app.Hesap.Kullanilabilir = true;
        app.Hesap.Basarili = false;
        var response = await PostForm("/Hesap/Giris", new() { ["Input.Eposta"] = "nisa@example.test", ["Input.Sifre"] = "Yanlis123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("E-posta veya şifre hatalı", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Theory]
    [InlineData("https://evil.example/", "/Egitimler")]
    [InlineData("//evil.example/", "/Egitimler")]
    [InlineData("/Egitimler/Detay/1", "/Egitimler/Detay/1")]
    public async Task BasariliGirisYalnizcaYerelAdreseDoner(string returnUrl, string expected)
    {
        app.Hesap.Kullanilabilir = true;
        var response = await PostForm("/Hesap/Giris", new()
        {
            ["Input.Eposta"] = "nisa@example.test", ["Input.Sifre"] = "Guvenli123!", ["ReturnUrl"] = returnUrl
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expected, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task BasariliKayitGiriseYonlendirir()
    {
        app.Hesap.Kullanilabilir = true;
        var response = await PostForm("/Hesap/Kaydol", new()
        {
            ["Input.Eposta"] = "nisa@example.test", ["Input.Sifre"] = "Guvenli123!", ["Input.SifreTekrar"] = "Guvenli123!"
        });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Hesap/Giris", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task CikisGetIleYapilmazVePostAntiforgeryGerektirir()
    {
        app.Hesap.Kullanilabilir = true;
        client.DefaultRequestHeaders.Add("X-Test-User", "nisa@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Hesap/Cikis")).StatusCode);
        Assert.Equal(0, app.Hesap.CikisCagrilari);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Hesap/Cikis", new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(0, app.Hesap.CikisCagrilari);
        Assert.Equal(HttpStatusCode.Redirect, (await PostForm("/Hesap/Cikis", new())).StatusCode);
        Assert.Equal(1, app.Hesap.CikisCagrilari);
    }

    [Fact]
    public async Task NavbarOturumluKullaniciyiGuvenliGosterir()
    {
        client.DefaultRequestHeaders.Add("X-Test-User", "<script>alert(1)</script>");
        var html = await client.GetStringAsync("/Egitimler");
        Assert.Contains("nav-user", html);
        Assert.DoesNotContain("<script>alert(1)</script>", html);
        Assert.Contains("action=\"/Hesap/Cikis\"", html);
    }

    [Theory]
    [InlineData("/Egitimler", HttpStatusCode.ServiceUnavailable)]
    [InlineData("/Egitimler/Detay/1", HttpStatusCode.ServiceUnavailable)]
    [InlineData("/", HttpStatusCode.OK)]
    public async Task BaglantiHatasiTeknikAyrintiSizdirmadanGosterilir(string url, HttpStatusCode expected)
    {
        app.VeritabaniHatasiUret = true;
        var response = await client.GetAsync(url);
        Assert.Equal(expected, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("şu anda ulaşılamıyor", html);
        Assert.DoesNotContain("NpgsqlException", html);
        Assert.DoesNotContain("Test bağlantısı", html);
    }

    [Fact]
    public async Task GercekBeklemeServisiBasariUretmez()
    {
        var hesap = new BekleyenHesapServisi();
        Assert.False(hesap.Kullanilabilir);
        Assert.False((await hesap.KaydolAsync("nisa@example.test", "Guvenli123!")).Basarili);
        Assert.False((await hesap.GirisYapAsync("nisa@example.test", "Guvenli123!", false)).Basarili);
        Assert.False((await hesap.CikisYapAsync()).Basarili);
    }

    private async Task<HttpResponseMessage> PostForm(string url, Dictionary<string, string> values)
    {
        var html = await client.GetStringAsync(url);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
        return await client.PostAsync(url, new FormUrlEncodedContent(values));
    }

    public void Dispose() => client.Dispose();
}
