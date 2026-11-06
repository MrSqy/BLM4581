using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

public sealed class MonacoEditorTests
{
    private sealed class Runner : IGorevKodServisi, IMobilKodServisi
    {
        public string? Kod;
        public bool Test;
        public Gorev? Gorev;
        public int Calls;
        public Task<GorevKodSonucu> CalistirAsync(Gorev gorev, string kod, bool test, CancellationToken ct)
        {
            Kod = kod; Test = test; Gorev = gorev; Calls++;
            if (kod == "servis-hatasi") throw new InvalidOperationException("Test sandbox yok.");
            if (kod == "platform-hatasi") throw new System.ComponentModel.Win32Exception("setsid bulunamadı.");
            var state = kod switch { "yanlis" => "basarisiz", "hata" => "hata", "sure" => "zaman_asimi", _ => test ? "basarili" : "tamamlandi" };
            return Task.FromResult(new GorevKodSonucu(gorev.No, state is "basarili" or "tamamlandi", state,
                kod == "bos" ? "" : "<script>alert('cikti')</script>\n", kod == "hata" ? "SyntaxError: test" : "", 0,
                test ? state == "basarili" : null, test ? true : null, []));
        }
        public Task<MobilKodSonucu> CalistirAsync(string kod, string testKodu, bool test, int[] gorevler, CancellationToken ct)
        {
            Kod = kod; Test = test; Calls++;
            return Task.FromResult(new MobilKodSonucu(true, test ? "basarili" : "tamamlandi", "eski ders", "",
                test ? gorevler.Select(n => new GorevTestSonucu(n, "basarili")).ToArray() : []));
        }
    }

    private static async Task<string> Token(HttpClient client, int id = 1)
    {
        var html = await client.GetStringAsync($"/Egitimler/Detay/{id}");
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        return WebUtility.HtmlDecode(token);
    }

    private static Task<HttpResponseMessage> Post(HttpClient client, string token, string action, object body, int id = 1, int? no = 1)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/Egitimler/Detay/{id}?handler={action}" + (no.HasValue ? $"&no={no}" : ""))
        { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", token);
        return client.SendAsync(request);
    }

    [Fact]
    public async Task EditorGercekGorevSablonlariniVeYerelMonacoyuSunar()
    {
        using var app = new IdentityApplication(); using var client = await app.IstemciAsync();
        var html = await client.GetStringAsync("/Egitimler/Detay/1");
        Assert.Contains("id=\"monaco-host\"", html);
        Assert.Contains("id=\"run-code\"", html);
        Assert.Contains("id=\"test-code\"", html);
        var json = Regex.Match(html, "<script id=\"code-editor-config\" type=\"application/json\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        using var config = JsonDocument.Parse(json);
        Assert.True(config.RootElement.GetProperty("gorevBazli").GetBoolean());
        Assert.True(config.RootElement.GetProperty("izinli").GetBoolean());
        var tasks = config.RootElement.GetProperty("gorevler").EnumerateArray().ToArray();
        Assert.Equal(7, tasks.Length);
        Assert.Equal(5, tasks.Count(t => t.GetProperty("tur").GetString() == "kod"));
        using var scope = app.Services.CreateScope();
        var first = await scope.ServiceProvider.GetRequiredService<UygulamaDbContext>().Gorevler.SingleAsync(g => g.EgitimId == 1 && g.No == 1);
        Assert.Equal(first.BaslangicKodu, tasks[0].GetProperty("baslangicKodu").GetString());
        Assert.False(tasks[5].GetProperty("kontrolVar").GetBoolean());
        foreach (var asset in new[] { "/lib/monaco/editor.js", "/lib/monaco/editor.worker.js", "/lib/monaco/editor.css", "/js/kod-editoru.js", "/css/kod-editoru.css" })
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(asset)).StatusCode);
    }

    [Fact]
    public async Task BaslangicKoduJsonScriptiniKapatamaz()
    {
        using var app = new IdentityApplication(); using var client = await app.IstemciAsync();
        const string source = "</script><script>alert('xss')</script>";
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
            (await db.Gorevler.SingleAsync(g => g.EgitimId == 1 && g.No == 1)).BaslangicKodu = source;
            await db.SaveChangesAsync();
        }
        var html = await client.GetStringAsync("/Egitimler/Detay/1");
        Assert.DoesNotContain(source, html);
        var json = Regex.Match(html, "<script id=\"code-editor-config\" type=\"application/json\">(.*?)</script>", RegexOptions.Singleline).Groups[1].Value;
        using var config = JsonDocument.Parse(json);
        Assert.Equal(source, config.RootElement.GetProperty("gorevler")[0].GetProperty("baslangicKodu").GetString());
    }

    [Theory]
    [InlineData("Calistir", false)]
    [InlineData("Test", true)]
    public async Task DogruServiseKodVeSunucudakiGoreviGonderir(string action, bool test)
    {
        var runner = new Runner(); using var app = new IdentityApplication(runner: runner, gorevRunner: runner);
        using var client = await app.IstemciAsync(); var token = await Token(client);
        var response = await Post(client, token, action, new { kod = "print('x')", kontrol = "sahte", baslangicDosyasi = "../fake.py" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("print('x')", runner.Kod); Assert.Equal(test, runner.Test); Assert.Equal(1, runner.Calls);
        Assert.Equal("gorevler/selam.py", runner.Gorev!.BaslangicDosyasi);
        Assert.DoesNotContain("sahte", runner.Gorev.KontrolJson!);
        var result = await response.Content.ReadFromJsonAsync<GorevKodSonucu>();
        Assert.Equal(test ? "basarili" : "tamamlandi", result!.Durum);
    }

    [Theory]
    [InlineData("yanlis", "basarisiz", false)]
    [InlineData("hata", "hata", false)]
    [InlineData("sure", "zaman_asimi", false)]
    [InlineData("bos", "basarili", true)]
    public async Task SonucDurumlariniVeBosCiktiyiKorur(string kod, string state, bool success)
    {
        var runner = new Runner(); using var app = new IdentityApplication(gorevRunner: runner); using var client = await app.IstemciAsync();
        var response = await Post(client, await Token(client), "Test", new { kod });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GorevKodSonucu>();
        Assert.Equal(state, result!.Durum); Assert.Equal(success, result.Basarili);
        if (kod == "bos") Assert.Equal("", result.Cikti);
    }

    [Fact]
    public async Task AntiforgeryVeBosBuyukKodKontrolleri()
    {
        var runner = new Runner(); using var app = new IdentityApplication(gorevRunner: runner); using var client = await app.IstemciAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/Egitimler/Detay/1?handler=Test&no=1", new { kod = "pass" })).StatusCode);
        var token = await Token(client);
        foreach (var kod in new[] { " ", new string('x', 32001) })
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, token, "Test", new { kod })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(client, token, "Test", new { kod = "pass" }, no: 8)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(client, token, "Test", new { kod = "pass" }, no: 6)).StatusCode);
        Assert.Equal(0, runner.Calls);
    }

    [Fact]
    public async Task KorunanDersWebCookieIsterVeEskiDersServisiniKullanir()
    {
        var runner = new Runner(); using var app = new IdentityApplication(runner: runner, gorevRunner: runner); using var client = await app.IstemciAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(client, await Token(client, 2), "Calistir", new { kod = "pass" }, id: 2, no: null)).StatusCode);
        Assert.Equal(0, runner.Calls);
        await IdentityApplication.KayitAsync(client); await IdentityApplication.GirisAsync(client);
        var token = await Token(client, 2);
        foreach (var action in new[] { "Calistir", "Test" })
            Assert.Equal(HttpStatusCode.OK, (await Post(client, token, action, new { kod = "pass" }, id: 2, no: null)).StatusCode);
        Assert.Equal(2, runner.Calls); Assert.True(runner.Test); Assert.Null(runner.Gorev);
    }

    [Theory]
    [InlineData("servis-hatasi")]
    [InlineData("platform-hatasi")]
    public async Task SandboxEksigiBasariSayilmaz(string kod)
    {
        var runner = new Runner(); using var app = new IdentityApplication(gorevRunner: runner); using var client = await app.IstemciAsync();
        var response = await Post(client, await Token(client), "Test", new { kod });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.DoesNotContain("Test sandbox yok", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EditorPostOrtakIstekSiniriniAsamaz()
    {
        var runner = new Runner(); using var app = new IdentityApplication(gorevRunner: runner); using var client = await app.IstemciAsync();
        var token = await Token(client);
        HttpResponseMessage? response = null;
        for (var n = 0; n < 31; n++)
        {
            response?.Dispose();
            response = await Post(client, token, "Calistir", new { kod = "pass" });
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, response!.StatusCode);
        Assert.True(runner.Calls < 31);
        response.Dispose();
    }
}
