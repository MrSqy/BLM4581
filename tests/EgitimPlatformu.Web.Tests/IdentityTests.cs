using System.Net;
using System.Text.RegularExpressions;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

public sealed class IdentityApplication : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public IdentityApplication() => connection.Open();
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseStaticWebAssets();
        builder.UseSetting("ConnectionStrings:Varsayilan", "Host=unused;Database=unused");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<UygulamaDbContext>();
            services.RemoveAll<DbContextOptions<UygulamaDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<UygulamaDbContext>>();
            services.AddDbContext<UygulamaDbContext>(o => o.UseSqlite(connection));
        });
    }
    public async Task<HttpClient> IstemciAsync()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost")
        });
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<UygulamaDbContext>().Database.EnsureCreatedAsync();
        var roller = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var rol in new[] { Roller.Ogrenci, Roller.Yonetici })
            if (!await roller.RoleExistsAsync(rol)) Assert.True((await roller.CreateAsync(new IdentityRole(rol))).Succeeded);
        return client;
    }
    public static async Task<HttpResponseMessage> FormAsync(HttpClient client, string path, Dictionary<string, string> values)
    {
        var html = await client.GetStringAsync(path);
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(token);
        return await client.PostAsync(path, new FormUrlEncodedContent(values));
    }
    public static Task<HttpResponseMessage> KayitAsync(HttpClient client, string email = "ogrenci@example.test")
        => FormAsync(client, "/Hesap/Kaydol", new()
        {
            ["Input.Eposta"] = email, ["Input.Sifre"] = "Guvenli123!", ["Input.SifreTekrar"] = "Guvenli123!"
        });
    public static Task<HttpResponseMessage> GirisAsync(HttpClient client, string email = "ogrenci@example.test", string password = "Guvenli123!")
        => FormAsync(client, "/Hesap/Giris", new() { ["Input.Eposta"] = email, ["Input.Sifre"] = password });
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}

public sealed class IdentityTests
{
    [Fact]
    public async Task GercekKayitRolParolaOzetVeOturumDongusu()
    {
        using var app = new IdentityApplication();
        using var client = await app.IstemciAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.KayitAsync(client)).StatusCode);
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = await users.FindByEmailAsync("ogrenci@example.test");
            Assert.NotNull(user);
            Assert.NotEqual("Guvenli123!", user.PasswordHash);
            Assert.True(await users.CheckPasswordAsync(user, "Guvenli123!"));
            Assert.True(await users.IsInRoleAsync(user, Roller.Ogrenci));
            Assert.False(await users.IsInRoleAsync(user, Roller.Yonetici));
        }
        Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.GirisAsync(client)).StatusCode);
        Assert.Contains("ogrenci@example.test", await client.GetStringAsync("/"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Hesap/Cikis", new FormUrlEncodedContent([]))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.FormAsync(client, "/Hesap/Cikis", new())).StatusCode);
        Assert.DoesNotContain("nav-user", await client.GetStringAsync("/"));
    }

    [Fact]
    public async Task TekrarKayitZayifParolaVeKilitleme()
    {
        using var app = new IdentityApplication();
        using var client = await app.IstemciAsync();
        await IdentityApplication.KayitAsync(client);
        Assert.Equal(HttpStatusCode.OK, (await IdentityApplication.KayitAsync(client)).StatusCode);
        var weak = await IdentityApplication.FormAsync(client, "/Hesap/Kaydol", new()
        {
            ["Input.Eposta"] = "baska@example.test", ["Input.Sifre"] = "abcdefgh", ["Input.SifreTekrar"] = "abcdefgh"
        });
        Assert.Equal(HttpStatusCode.OK, weak.StatusCode);
        for (var i = 0; i < 5; i++) await IdentityApplication.GirisAsync(client, password: "Yanlis123!");
        Assert.Equal(HttpStatusCode.OK, (await IdentityApplication.GirisAsync(client)).StatusCode);
        using var scope = app.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await users.FindByEmailAsync("ogrenci@example.test");
        Assert.True(await users.IsLockedOutAsync(user!));
        Assert.Null(await users.FindByEmailAsync("baska@example.test"));
    }

    [Fact]
    public async Task BaslangicYoneticisiTekrarlanabilirVeOgrenciyiYukseltmez()
    {
        using var app = new IdentityApplication();
        using var client = await app.IstemciAsync();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BaslangicYoneticisi:Eposta"] = "yonetici@example.test",
            ["BaslangicYoneticisi:Sifre"] = "Guvenli123!"
        }).Build();
        await IdentityBaslangic.HazirlaAsync(app.Services, config);
        await IdentityBaslangic.HazirlaAsync(app.Services, config);
        using (var scope = app.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True(await users.IsInRoleAsync((await users.FindByEmailAsync("yonetici@example.test"))!, Roller.Yonetici));
            Assert.Equal(1, await users.Users.CountAsync());
        }
        await IdentityApplication.KayitAsync(client);
        config["BaslangicYoneticisi:Eposta"] = "ogrenci@example.test";
        await Assert.ThrowsAsync<InvalidOperationException>(() => IdentityBaslangic.HazirlaAsync(app.Services, config));
    }
}
