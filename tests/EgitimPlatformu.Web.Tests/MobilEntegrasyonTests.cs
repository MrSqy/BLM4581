using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace EgitimPlatformu.Web.Tests;

public class MobilEntegrasyonTests
{
    private sealed class Runner : IMobilKodServisi
    {
        public int Calls;
        public string? TestKodu;
        public bool Fail;
        public Task<MobilKodSonucu> CalistirAsync(string kod, string testKodu, bool test, int[] gorevler, CancellationToken ct)
        {
            Calls++; TestKodu = testKodu;
            if (Fail) throw new InvalidOperationException();
            return Task.FromResult(new MobilKodSonucu(true, "tamamlandi", "ok", "", []));
        }
    }
    private static async Task<string> Kayit(HttpClient c)
    {
        var response = await c.PostAsJsonAsync("/api/mobil/hesap/kayit", new { adSoyad = "Test Öğrenci", eposta = "mobil@example.test", sifre = "Guvenli123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Test Öğrenci", json.GetProperty("kullanici").GetProperty("adSoyad").GetString());
        Assert.InRange(json.GetProperty("bitis").GetDateTime(), DateTime.UtcNow.AddMinutes(14), DateTime.UtcNow.AddMinutes(16));
        return json.GetProperty("token").GetString()!;
    }
    [Fact]
    public async Task MisafirSiniriHerIkiApiVeRunnerIcinGecerli()
    {
        var runner = new Runner(); using var app = new IdentityApplication(runner: runner); using var c = await app.IstemciAsync();
        foreach (var prefix in new[] { "/api/egitimler", "/api/mobil/egitimler" })
        {
            Assert.Equal(HttpStatusCode.OK, (await c.GetAsync(prefix + "/1")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync(prefix + "/2")).StatusCode);
        }
        var list = await c.GetStringAsync("/api/mobil/egitimler");
        Assert.DoesNotContain("icerikMarkdown", list); Assert.DoesNotContain("testKodu", list); Assert.DoesNotContain("baslangicKodu", list);
        foreach (var action in new[] { "calistir", "test" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync($"/api/mobil/egitimler/2/{action}", new { kod = "pass" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync($"/api/mobil/egitimler/1/{action}", new { kod = "pass", testKodu = "CLIENT_TEST" })).StatusCode);
        }
        Assert.Equal(2, runner.Calls); Assert.NotEqual("CLIENT_TEST", runner.TestKodu); Assert.False(string.IsNullOrEmpty(runner.TestKodu));
        runner.Fail = true;
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.PostAsJsonAsync("/api/mobil/egitimler/1/calistir", new { kod = "pass" })).StatusCode);
    }
    [Fact]
    public async Task MobilKayitOrtakTokenVeCikisTumTokenlariIptalEder()
    {
        var runner = new Runner(); using var app = new IdentityApplication(runner: runner); using var c = await app.IstemciAsync();
        c.DefaultRequestHeaders.Authorization = new("Bearer", await Kayit(c));
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/mobil/hesap/ben")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/egitimler/2")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.GetAsync("/api/mobil/egitimler/2")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.PostAsJsonAsync("/api/mobil/egitimler/2/test", new { kod = "pass" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.DeleteAsync("/api/egitimler/2")).StatusCode);
        var second = await c.PostAsJsonAsync("/api/hesap/giris", new { eposta = "mobil@example.test", sifre = "Guvenli123!" });
        var token = (await second.Content.ReadFromJsonAsync<TokenYaniti>())!.AccessToken;
        Assert.Equal(HttpStatusCode.NoContent, (await c.PostAsJsonAsync("/api/mobil/hesap/cikis", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/mobil/hesap/ben")).StatusCode);
        c.DefaultRequestHeaders.Authorization = new("Bearer", token);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.GetAsync("/api/egitimler/2")).StatusCode);
        var login = await c.PostAsJsonAsync("/api/mobil/hesap/giris", new { eposta = "mobil@example.test", sifre = "Guvenli123!" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
    [Fact]
    public async Task MobilGirisIkiFaktoruAtlayamazVeEksikAnahtarKayitAcmaz()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync(); await Kayit(c);
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        await users.SetTwoFactorEnabledAsync((await users.FindByEmailAsync("mobil@example.test"))!, true);
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.PostAsJsonAsync("/api/mobil/hesap/giris", new { eposta = "mobil@example.test", sifre = "Guvenli123!" })).StatusCode);
        var config = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();
        config["Jwt:Anahtar"] = "";
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await c.PostAsJsonAsync("/api/mobil/hesap/kayit", new { adSoyad = "Test", eposta = "yeni@example.test", sifre = "Guvenli123!" })).StatusCode);
        Assert.Null(await users.FindByEmailAsync("yeni@example.test"));
    }
}
