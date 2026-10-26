using System.Security.Claims;
using System.Data.Common;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace EgitimPlatformu.Web.Tests;

public sealed class TestApplication : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public TestHesapServisi Hesap { get; } = new();
    public bool VeritabaniHatasiUret { get; set; }

    public TestApplication() => connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseStaticWebAssets();
        // Sadece test verisi: gerçek user-secrets veya PostgreSQL kullanılmaz.
        builder.UseSetting("ConnectionStrings:Varsayilan", "Host=unused;Database=unused");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<UygulamaDbContext>();
            services.RemoveAll<DbContextOptions<UygulamaDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<UygulamaDbContext>>();
            services.AddDbContext<UygulamaDbContext>(options => options.UseSqlite(connection)
                .AddInterceptors(new TestBaglantiHatasi(() => VeritabaniHatasiUret)));
            services.RemoveAll<IHesapServisi>();
            services.AddSingleton<IHesapServisi>(Hesap);
            services.AddSingleton<IStartupFilter, TestKullaniciFiltresi>();
        });
    }

    public HttpClient HazirIstemci()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<UygulamaDbContext>().Database.EnsureCreated();
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}

internal sealed class TestBaglantiHatasi(Func<bool> hataUret) : DbCommandInterceptor
{
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (hataUret()) throw new InvalidOperationException("Geçici bağlantı hatası.", new Npgsql.NpgsqlException("Test bağlantısı kullanılamıyor."));
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }
}

// Bu kimlik yalnızca test sunucusunda kurulur, üretim koduna dahil edilmez.
internal sealed class TestKullaniciFiltresi : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, sonraki) =>
        {
            if (context.Request.Headers.TryGetValue("X-Test-User", out var name))
                context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, name.ToString())], "Test"));
            await sonraki();
        });
        next(app);
    };
}

public sealed class TestHesapServisi : IHesapServisi
{
    public bool Kullanilabilir { get; set; }
    public bool Basarili { get; set; } = true;
    public int CikisCagrilari { get; set; }
    public Task<HesapSonucu> KaydolAsync(string eposta, string sifre) => Task.FromResult(new HesapSonucu(Basarili, Basarili ? "Hesabın oluşturuldu. Giriş yapabilirsin." : "Kayıt tamamlanamadı."));
    public Task<HesapSonucu> GirisYapAsync(string eposta, string sifre, bool beniHatirla) => Task.FromResult(new HesapSonucu(Basarili, Basarili ? "Oturum açıldı." : "E-posta veya şifre hatalı."));
    public Task<HesapSonucu> CikisYapAsync()
    {
        CikisCagrilari++;
        return Task.FromResult(new HesapSonucu(Basarili, Basarili ? "Oturum kapatıldı." : "Çıkış tamamlanamadı."));
    }
}
