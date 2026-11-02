using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
namespace EgitimPlatformu.Web.Tests;

public class HaftaUcTests
{
    private static EgitimYazma Ders() => new() { KategoriId = 1, Kod = "PY-99", Baslik = "API dersi", IcerikMarkdown = "# Ders", BaslangicKodu = "pass", TestKodu = "GIZLI_TEST" };
    private static async Task<string> Token(IdentityApplication app, HttpClient client, bool admin = true)
    {
        await IdentityApplication.KayitAsync(client);
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            if (admin) Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync("ogrenci@example.test"))!, Roller.Yonetici)).Succeeded);
        }
        var response = await client.PostAsJsonAsync("/api/hesap/giris", new { eposta = "ogrenci@example.test", sifre = "Guvenli123!" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.False(response.Headers.Contains("Set-Cookie"));
        var token = (await response.Content.ReadFromJsonAsync<TokenYaniti>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return token.AccessToken;
    }
    [Fact]
    public async Task CrudTumAkisVeGorevSilme()
    {
        using var app = new IdentityApplication(); using var client = await app.IstemciAsync();
        await Token(app, client);
        var response = await client.PostAsJsonAsync("/api/egitimler", Ders());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var dto = (await response.Content.ReadFromJsonAsync<EgitimDetay>())!;
        Assert.EndsWith($"/api/egitimler/{dto.Id}", response.Headers.Location!.ToString());
        Assert.DoesNotContain("GIZLI_TEST", await response.Content.ReadAsStringAsync());
        var update = Ders(); update.Baslik = "Yeni başlık";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/egitimler/{dto.Id}", update)).StatusCode);
        Assert.Equal("Yeni başlık", (await client.GetFromJsonAsync<EgitimDetay>($"/api/egitimler/{dto.Id}"))!.Baslik);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
            db.Gorevler.Add(new Gorev { EgitimId = dto.Id, No = 1, Baslik = "Görev" }); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/egitimler/{dto.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/egitimler/{dto.Id}")).StatusCode);
        using var check = app.Services.CreateScope();
        Assert.False(await check.ServiceProvider.GetRequiredService<UygulamaDbContext>().Gorevler.AnyAsync(g => g.EgitimId == dto.Id));
    }
    [Fact]
    public async Task ListeFiltreSayfalamaVeTestKoduGizliligi()
    {
        using var app = new IdentityApplication(); using var client = await app.IstemciAsync();
        var liste = (await client.GetFromJsonAsync<EgitimSayfasi>("/api/egitimler?kategoriId=1&sayfaBoyutu=1&sayfa=2"))!;
        Assert.Equal(3, liste.Toplam); Assert.Single(liste.Egitimler); Assert.Equal("PY-02", liste.Egitimler[0].Kod);
        Assert.Equal(1, (await client.GetFromJsonAsync<EgitimSayfasi>("/api/egitimler?q=py-03"))!.Toplam);
        var detail = await client.GetStringAsync("/api/egitimler/1");
        Assert.DoesNotContain("testKodu", detail); Assert.DoesNotContain("@pytest", detail);
        Assert.Equal(8, JsonSerializer.Deserialize<EgitimDetay>(detail, new JsonSerializerOptions(JsonSerializerDefaults.Web))!.Gorevler.Count);
    }
    [Theory]
    [InlineData("?sayfa=0")][InlineData("?sayfaBoyutu=101")][InlineData("?kategoriId=-1")][InlineData("?sayfa=abc")]
    public async Task GecersizSorgu400(string query)
    {
        using var app = new IdentityApplication(); using var client = await app.IstemciAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/egitimler" + query)).StatusCode);
    }
    [Fact]
    public async Task AnonimCookieVeOgrenciYazamaz()
    {
        using var app = new IdentityApplication(); using var client = await app.IstemciAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/egitimler", Ders())).StatusCode);
        await Token(app, client, admin: false);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/egitimler", Ders())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync("/api/egitimler/1")).StatusCode);
        client.DefaultRequestHeaders.Authorization = null;
        await IdentityApplication.GirisAsync(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/egitimler", Ders())).StatusCode);
    }
    [Fact]
    public async Task EksikAlanKategoriVeTekrarKodReddedilir()
    {
        using var app = new IdentityApplication(); using var client = await app.IstemciAsync(); await Token(app, client);
        var ders = Ders(); ders.KategoriId = 999;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/egitimler", ders)).StatusCode);
        ders.KategoriId = 1; ders.Kod = "PY-01";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/egitimler", ders)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/egitimler/2", ders)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsync("/api/egitimler/1", new StringContent("{\"baslik\":\"Yanlış\"}", Encoding.UTF8, "application/json"))).StatusCode);
        Assert.NotEmpty((await client.GetFromJsonAsync<EgitimDetay>("/api/egitimler/1"))!.IcerikMarkdown);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync("/api/egitimler/999", Ders())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync("/api/egitimler/999")).StatusCode);
    }
    [Theory]
    [InlineData("expired")][InlineData("issuer")][InlineData("audience")][InlineData("key")]
    public async Task GecersizToken401(string hata)
    {
        using var app = new IdentityApplication(); using var client = await app.IstemciAsync();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(await Token(app, client));
        var key = hata == "key" ? new byte[32] : Convert.FromBase64String(app.JwtAnahtari);
        var forged = new JwtSecurityToken(hata == "issuer" ? "wrong" : JwtServisi.Issuer,
            hata == "audience" ? "wrong" : JwtServisi.Audience, token.Claims.Where(c => c.Type is not ("aud" or "iss" or "exp" or "nbf")),
            DateTime.UtcNow.AddHours(-2), hata == "expired" ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(forged));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/egitimler/1")).StatusCode);
    }
    [Theory]
    [InlineData("stamp")][InlineData("role")][InlineData("lock")]
    public async Task HesapDegisinceTokenGecersiz(string degisiklik)
    {
        using var app = new IdentityApplication(); using var client = await app.IstemciAsync(); await Token(app, client);
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = (await users.FindByEmailAsync("ogrenci@example.test"))!;
            if (degisiklik == "stamp") await users.UpdateSecurityStampAsync(user);
            if (degisiklik == "role") await users.RemoveFromRoleAsync(user, Roller.Yonetici);
            if (degisiklik == "lock") await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5));
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.DeleteAsync("/api/egitimler/1")).StatusCode);
    }
    [Fact]
    public async Task MobilYanlisSifreKilitlemeVeSwagger()
    {
        using var app = new IdentityApplication(swagger: true); using var client = await app.IstemciAsync();
        await IdentityApplication.KayitAsync(client);
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsJsonAsync("/api/hesap/giris", new { eposta = "ogrenci@example.test", sifre = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/hesap/giris", new { eposta = "ogrenci@example.test", sifre = "Guvenli123!" })).StatusCode);
        var swagger = await client.GetStringAsync("/swagger/v1/swagger.json");
        using var doc = JsonDocument.Parse(swagger);
        Assert.True(doc.RootElement.GetProperty("paths").GetProperty("/api/egitimler").GetProperty("post").TryGetProperty("security", out _));
        Assert.False(doc.RootElement.GetProperty("paths").GetProperty("/api/egitimler").GetProperty("get").TryGetProperty("security", out _));
        Assert.Contains("swagger-ui", await client.GetStringAsync("/swagger/index.html"));
    }
    [Fact]
    public async Task SwaggerGelistirmeDisindaKapali()
    {
        using var app = new IdentityApplication(); using var client = await app.IstemciAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
    }
}
