using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

// Mobil 6.3'ün bağlandığı /api/notebook/oturum WebSocket ucu.
public sealed class NotebookOturumuTests
{
    [Fact]
    public async Task HucrelerCalisirVeSonuclarHucreHucreGelir()
    {
        var servis = new SahteNotebook();
        await using var baglanti = await BaglanAsync(servis);
        var hazir = await baglanti.AlAsync();
        Assert.Equal("hazir", Tur(hazir));
        Assert.Equal(32, hazir.GetProperty("azamiHucre").GetInt32());

        await baglanti.GonderAsync(new { tur = "calistir", istekId = "a1", hucreler = new[] { "x = 40", "print(x + 2)" } });
        var basladi = await baglanti.AlAsync();
        Assert.Equal("basladi", Tur(basladi));
        Assert.Equal(2, basladi.GetProperty("hucreSayisi").GetInt32());

        var ilk = await baglanti.AlAsync();
        Assert.Equal(("hucre", 0, "ok"), (Tur(ilk), ilk.GetProperty("sira").GetInt32(), ilk.GetProperty("durum").GetString()));
        var ikinci = await baglanti.AlAsync();
        Assert.Equal("42\n", ikinci.GetProperty("ciktilar")[0].GetProperty("text").GetString());
        Assert.Equal("a1", ikinci.GetProperty("istekId").GetString());

        var bitti = await baglanti.AlAsync();
        Assert.Equal(("bitti", "tamamlandi"), (Tur(bitti), bitti.GetProperty("durum").GetString()));
        Assert.Equal(["x = 40", "print(x + 2)"], Assert.Single(servis.Cagrilar));
    }

    [Fact]
    public async Task TokenOlmadanElSikismaReddedilir()
    {
        using var app = Uygulama(new SahteNotebook());
        await HttpAsync(app);
        var istemci = app.Server.CreateWebSocketClient();
        var hata = await Assert.ThrowsAnyAsync<Exception>(() =>
            istemci.ConnectAsync(new Uri("wss://localhost/api/notebook/oturum"), CancellationToken.None));
        Assert.Contains("401", hata.Message);
    }

    [Fact]
    public async Task WebSocketOlmayanIstek400Alir()
    {
        using var app = Uygulama(new SahteNotebook());
        var http = await HttpAsync(app);
        http.DefaultRequestHeaders.Authorization = new("Bearer", await TokenAsync(http));
        var yanit = await http.GetAsync("/api/notebook/oturum");
        Assert.Equal(HttpStatusCode.BadRequest, yanit.StatusCode);
    }

    [Theory]
    [InlineData("""{"tur":"calistir","istekId":"b","hucreler":[]}""", "1 ile 32")]
    [InlineData("""{"tur":"calistir","istekId":"b","hucreler":"print(1)"}""", "metin dizisi")]
    [InlineData("""{"tur":"calistir","istekId":"b","hucreler":[1]}""", "yalnızca metin")]
    [InlineData("""{"tur":"bilinmeyen","istekId":"b"}""", "Bilinmeyen")]
    public async Task GecersizIstekHataDonerVeOturumAcikKalir(string mesaj, string beklenen)
    {
        await using var baglanti = await BaglanAsync(new SahteNotebook());
        await baglanti.AlAsync();
        await baglanti.HamGonderAsync(mesaj);
        var hata = await baglanti.AlAsync();
        Assert.Equal(("hata", "gecersiz_istek", "b"), (Tur(hata), hata.GetProperty("kod").GetString(), hata.GetProperty("istekId").GetString()));
        Assert.Contains(beklenen, hata.GetProperty("mesaj").GetString());

        await baglanti.GonderAsync(new { tur = "ping", istekId = "p" });
        Assert.Equal("pong", Tur(await baglanti.AlAsync()));
    }

    [Fact]
    public async Task OtuzIkidenFazlaHucreReddedilir()
    {
        await using var baglanti = await BaglanAsync(new SahteNotebook());
        await baglanti.AlAsync();
        await baglanti.GonderAsync(new { tur = "calistir", istekId = "c", hucreler = Enumerable.Repeat("1", 33) });
        Assert.Equal("gecersiz_istek", (await baglanti.AlAsync()).GetProperty("kod").GetString());
    }

    [Fact]
    public async Task CalismaSurerkenYeniIstekMesgulAlirVeDurdurIptalEder()
    {
        var servis = new SahteNotebook { Bekle = true };
        await using var baglanti = await BaglanAsync(servis);
        await baglanti.AlAsync();
        await baglanti.GonderAsync(new { tur = "calistir", istekId = "u", hucreler = new[] { "import time; time.sleep(60)" } });
        Assert.Equal("basladi", Tur(await baglanti.AlAsync()));

        await baglanti.GonderAsync(new { tur = "calistir", istekId = "v", hucreler = new[] { "1" } });
        var mesgul = await baglanti.AlAsync();
        Assert.Equal(("hata", "mesgul", "v"), (Tur(mesgul), mesgul.GetProperty("kod").GetString(), mesgul.GetProperty("istekId").GetString()));

        await baglanti.GonderAsync(new { tur = "durdur" });
        var bitti = await baglanti.AlAsync();
        Assert.Equal(("bitti", "durduruldu", "u"), (Tur(bitti), bitti.GetProperty("durum").GetString(), bitti.GetProperty("istekId").GetString()));
        Assert.True(servis.IptalEdildi);

        servis.Bekle = false;
        await baglanti.GonderAsync(new { tur = "calistir", istekId = "w", hucreler = new[] { "1" } });
        Assert.Equal("basladi", Tur(await baglanti.AlAsync()));
    }

    [Theory]
    [InlineData("zaman_asimi")]
    [InlineData("altyapi_yok")]
    public async Task SandboxDurumuBittiMesajinaYansir(string durum)
    {
        await using var baglanti = await BaglanAsync(new SahteNotebook { Sonuc = new(durum, []) });
        await baglanti.AlAsync();
        await baglanti.GonderAsync(new { tur = "calistir", istekId = "z", hucreler = new[] { "1" } });
        await baglanti.AlAsync();
        Assert.Equal(durum, (await baglanti.AlAsync()).GetProperty("durum").GetString());
    }

    [Fact]
    public async Task KuyrukDoluysaAltyapiYokBildirilir()
    {
        await using var baglanti = await BaglanAsync(new SahteNotebook { Firlat = new InvalidOperationException("kuyruk dolu") });
        await baglanti.AlAsync();
        await baglanti.GonderAsync(new { tur = "calistir", istekId = "k", hucreler = new[] { "1" } });
        await baglanti.AlAsync();
        Assert.Equal("altyapi_yok", (await baglanti.AlAsync()).GetProperty("durum").GetString());
    }

    [Fact]
    public async Task BozukJsonOturumuKapatir()
    {
        await using var baglanti = await BaglanAsync(new SahteNotebook());
        await baglanti.AlAsync();
        await baglanti.HamGonderAsync("bozuk");
        var (durum, _) = await baglanti.KapanisAsync();
        Assert.Equal(WebSocketCloseStatus.InvalidPayloadData, durum);
    }

    [Fact]
    public async Task BuyukMesajOturumuKapatir()
    {
        await using var baglanti = await BaglanAsync(new SahteNotebook());
        await baglanti.AlAsync();
        await baglanti.HamGonderAsync(new string('x', NotebookOturumu.AzamiMesaj + 1));
        var (durum, _) = await baglanti.KapanisAsync();
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, durum);
    }

    private static string? Tur(JsonElement mesaj) => mesaj.GetProperty("tur").GetString();

    private static WebApplicationFactory<Program> Uygulama(SahteNotebook servis) =>
        new IdentityApplication().WithWebHostBuilder(b => b.ConfigureServices(s =>
        {
            s.RemoveAll<INotebookCalistirmaServisi>();
            s.AddSingleton<INotebookCalistirmaServisi>(servis);
        }));

    private static async Task<string> TokenAsync(HttpClient http)
    {
        var yanit = await http.PostAsJsonAsync("/api/mobil/hesap/kayit",
            new { adSoyad = "Notebook Öğrenci", eposta = "notebook@example.test", sifre = "Guvenli123!" });
        Assert.Equal(HttpStatusCode.OK, yanit.StatusCode);
        return (await yanit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }

    private static async Task<Baglanti> BaglanAsync(SahteNotebook servis)
    {
        var app = Uygulama(servis);
        var http = await HttpAsync(app);
        var token = await TokenAsync(http);
        var istemci = app.Server.CreateWebSocketClient();
        istemci.ConfigureRequest = istek => istek.Headers.Authorization = $"Bearer {token}";
        var soket = await istemci.ConnectAsync(new Uri("wss://localhost/api/notebook/oturum"), CancellationToken.None);
        return new Baglanti(app, soket);
    }

    // IdentityApplication'ın rol ve veritabanı hazırlığını WithWebHostBuilder kopyasında yapar.
    private static async Task<HttpClient> HttpAsync(WebApplicationFactory<Program> app)
    {
        var http = app.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EgitimPlatformu.Web.Data.UygulamaDbContext>();
        await db.Database.EnsureCreatedAsync();
        var roller = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.RoleManager<Microsoft.AspNetCore.Identity.IdentityRole>>();
        foreach (var rol in new[] { Roller.Ogrenci, Roller.Yonetici })
            if (!await roller.RoleExistsAsync(rol)) await roller.CreateAsync(new(rol));
        return http;
    }

    private sealed class Baglanti(WebApplicationFactory<Program> app, WebSocket soket) : IAsyncDisposable
    {
        public Task GonderAsync(object mesaj) => HamGonderAsync(JsonSerializer.Serialize(mesaj));

        public Task HamGonderAsync(string metin) =>
            soket.SendAsync(Encoding.UTF8.GetBytes(metin), WebSocketMessageType.Text, true, CancellationToken.None);

        public async Task<JsonElement> AlAsync()
        {
            using var zaman = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var tampon = new byte[64 * 1024];
            using var veri = new MemoryStream();
            WebSocketReceiveResult sonuc;
            do
            {
                sonuc = await soket.ReceiveAsync(tampon, zaman.Token);
                Assert.NotEqual(WebSocketMessageType.Close, sonuc.MessageType);
                veri.Write(tampon, 0, sonuc.Count);
            } while (!sonuc.EndOfMessage);
            return JsonDocument.Parse(veri.ToArray()).RootElement.Clone();
        }

        public async Task<(WebSocketCloseStatus?, string?)> KapanisAsync()
        {
            using var zaman = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var sonuc = await soket.ReceiveAsync(new byte[1024], zaman.Token);
            Assert.Equal(WebSocketMessageType.Close, sonuc.MessageType);
            return (sonuc.CloseStatus, sonuc.CloseStatusDescription);
        }

        public async ValueTask DisposeAsync()
        {
            if (soket.State == WebSocketState.Open)
                await soket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
            soket.Dispose();
            await app.DisposeAsync();
        }
    }

    private sealed class SahteNotebook : INotebookCalistirmaServisi
    {
        public List<string[]> Cagrilar { get; } = [];
        public volatile bool Bekle;
        public bool IptalEdildi { get; private set; }
        public NotebookCalistirmaSonucu? Sonuc { get; init; }
        public Exception? Firlat { get; init; }

        public async Task<NotebookCalistirmaSonucu> CalistirAsync(IReadOnlyList<string> hucreler, CancellationToken ct = default)
        {
            Cagrilar.Add([.. hucreler]);
            if (Firlat is not null) throw Firlat;
            if (Bekle)
            {
                try { await Task.Delay(Timeout.Infinite, ct); }
                catch (OperationCanceledException) { IptalEdildi = true; throw; }
            }
            if (Sonuc is not null) return Sonuc;
            var bos = JsonDocument.Parse("[]").RootElement;
            var cikti = JsonDocument.Parse("""[{"output_type":"stream","name":"stdout","text":"42\n"}]""").RootElement;
            return new("tamamlandi", hucreler.Select((_, i) =>
                new NotebookHucreSonucu("ok", i == 1 ? [.. cikti.EnumerateArray()] : [.. bos.EnumerateArray()])).ToArray());
        }
    }
}
