using System.Net;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

public class HaftaDortTests
{
    private static async Task Giris(WebApplicationFactory<Program> app, HttpClient client, string email, bool admin = false)
    {
        Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.KayitAsync(client, email)).StatusCode);
        if (admin)
        {
            using var scope = app.Services.CreateScope();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(Roller.Yonetici)) await roles.CreateAsync(new IdentityRole(Roller.Yonetici));
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByEmailAsync(email))!, Roller.Yonetici)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.GirisAsync(client, email)).StatusCode);
    }
    private static Task<HttpResponseMessage> Form(HttpClient c, string path, Dictionary<string,string> values)
        => IdentityApplication.FormAsync(c, path, values);
    private static Dictionary<string,string> Kategori(string name = "Yeni kategori", string prefix = "DEN") => new() { ["Input.Ad"] = name, ["Input.Kisaltma"] = prefix, ["Input.Sira"] = "1" };
    private static Dictionary<string,string> Egitim(string code = "DEN-01") => new() { ["Input.KategoriId"] = "1", ["Input.Kod"] = code, ["Input.Baslik"] = "Deneme dersi", ["Input.Sira"] = "4", ["Input.IcerikMarkdown"] = "# Deneme" };
    private static Dictionary<string,string> Gorev(int lesson = 1, int no = 9) => new() { ["Input.EgitimId"] = lesson.ToString(), ["Input.No"] = no.ToString(), ["Input.Baslik"] = "Yeni kod görevi", ["Input.Tur"] = "kod", ["Input.Aktif"] = "true", ["Input.BaslangicDosyasi"] = "gorevler/yeni.py", ["Input.BaslangicKodu"] = "# TODO", ["Input.BeklenenCikti"] = "selam\n", ["Input.BeklenenCikisKodu"] = "0" };

    [Theory]
    [InlineData("/Yonetim")][InlineData("/Yonetim/Kategoriler")][InlineData("/Yonetim/Egitimler")][InlineData("/Yonetim/Gorevler")]
    public async Task YonetimAnonimVeOgrenciyeKapali(string path)
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync();
        Assert.Equal(HttpStatusCode.Redirect, (await c.GetAsync(path)).StatusCode);
        await Giris(app,c,"ogrenci@example.test");
        var denied = await c.GetAsync(path); Assert.Equal(HttpStatusCode.Redirect,denied.StatusCode);
        Assert.Contains("/Hesap/Giris", denied.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await c.PostAsync(path, new FormUrlEncodedContent(Kategori()))).StatusCode);
    }

    [Fact]
    public async Task KategoriCrudDogrulamaCsrfVeIliskiliSilme()
    {
        using var app = new IdentityApplication(); using var c = await app.IstemciAsync(); await Giris(app,c,"admin@example.test",true);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsync("/Yonetim/Kategoriler",new FormUrlEncodedContent(Kategori()))).StatusCode);
        var invalid = await Form(c,"/Yonetim/Kategoriler",Kategori("", "kucuk"));
        Assert.Equal(HttpStatusCode.OK,invalid.StatusCode); Assert.Contains("Kategori adı zorunludur",WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()));
        Assert.Equal(HttpStatusCode.Redirect,(await Form(c,"/Yonetim/Kategoriler",Kategori())).StatusCode);
        using var scope = app.Services.CreateScope(); var db=scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        var id=(await db.Kategoriler.SingleAsync(k=>k.Kisaltma=="DEN")).Id;
        var edit=Kategori("Güncel kategori"); edit["DuzenleId"]=id.ToString();
        Assert.Equal(HttpStatusCode.Redirect,(await Form(c,$"/Yonetim/Kategoriler?DuzenleId={id}",edit)).StatusCode);
        Assert.Equal("Güncel kategori",await db.Kategoriler.Where(k=>k.Id==id).Select(k=>k.Ad).SingleAsync());
        Assert.Equal(HttpStatusCode.OK,(await Form(c,"/Yonetim/Kategoriler?handler=Sil",new(){["id"]=id.ToString()})).StatusCode);
        Assert.True(await db.Kategoriler.AnyAsync(k=>k.Id==id));
        Assert.Equal(HttpStatusCode.OK,(await Form(c,"/Yonetim/Kategoriler?handler=Sil",new(){["id"]="1",["Onay"]="true"})).StatusCode);
        Assert.True(await db.Kategoriler.AnyAsync(k=>k.Id==1));
        Assert.Equal(HttpStatusCode.Redirect,(await Form(c,"/Yonetim/Kategoriler?handler=Sil",new(){["id"]=id.ToString(),["Onay"]="true"})).StatusCode);
        Assert.False(await db.Kategoriler.AnyAsync(k=>k.Id==id));
    }

    [Fact]
    public async Task EgitimVeGorevCrudGuvenliKontrolVeCascade()
    {
        using var app=new IdentityApplication(); using var c=await app.IstemciAsync(); await Giris(app,c,"admin@example.test",true);
        Assert.Equal(HttpStatusCode.Redirect,(await Form(c,"/Yonetim/Egitimler",Egitim())).StatusCode);
        using var scope=app.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<UygulamaDbContext>();
        var id=(await db.Egitimler.SingleAsync(e=>e.Kod=="DEN-01")).Id;
        var edit=Egitim();edit["DuzenleId"]=id.ToString();edit["Input.Baslik"]="Değişen ders";
        Assert.Equal(HttpStatusCode.Redirect,(await Form(c,$"/Yonetim/Egitimler?DuzenleId={id}",edit)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Form(c,"/Yonetim/Egitimler",Egitim())).StatusCode); // benzersiz kod
        var bad=Gorev(id,1); bad["Input.BaslangicDosyasi"]="../tehlikeli.py";
        Assert.Equal(HttpStatusCode.OK,(await Form(c,"/Yonetim/Gorevler",bad)).StatusCode);
        Assert.False(await db.Gorevler.AnyAsync(g=>g.EgitimId==id));
        var browserText=Gorev(id,1);browserText["Input.BeklenenCikti"]="selam\r\n";
        Assert.Equal(HttpStatusCode.Redirect,(await Form(c,"/Yonetim/Gorevler",browserText)).StatusCode);
        var taskId=(await db.Gorevler.SingleAsync(g=>g.EgitimId==id)).Id;
        var taskEdit=Gorev(id,1);taskEdit["DuzenleId"]=taskId.ToString();taskEdit["Input.Baslik"]="Düzenlenen görev";
        Assert.Equal(HttpStatusCode.Redirect,(await Form(c,$"/Yonetim/Gorevler?DuzenleId={taskId}",taskEdit)).StatusCode);
        Assert.Contains("selam\\n",(await db.Gorevler.AsNoTracking().SingleAsync(g=>g.Id==taskId)).KontrolJson!);
        Assert.Equal(HttpStatusCode.OK,(await Form(c,"/Yonetim/Gorevler",Gorev(id,1))).StatusCode); // yinelenen no
        Assert.Equal(HttpStatusCode.Redirect,(await Form(c,"/Yonetim/Gorevler?handler=Sil",new(){["id"]=taskId.ToString(),["Onay"]="true"})).StatusCode);
        Assert.False(await db.Gorevler.AnyAsync(g=>g.Id==taskId));
        Assert.Equal(HttpStatusCode.Redirect,(await Form(c,"/Yonetim/Gorevler",Gorev(id,1))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Form(c,"/Yonetim/Egitimler?handler=Sil",new(){["id"]=id.ToString(),["Onay"]="true"})).StatusCode);
        Assert.False(await db.Egitimler.AnyAsync(e=>e.Id==id)); Assert.False(await db.Gorevler.AnyAsync(g=>g.EgitimId==id));
    }

    [Fact]
    public async Task AciklamaTuruVeKaristirilanDersReddedilir()
    {
        using var app=new IdentityApplication(); using var c=await app.IstemciAsync();await Giris(app,c,"admin@example.test",true);
        var bad=Gorev();bad["Input.Tur"]="aciklama";
        Assert.Equal(HttpStatusCode.OK,(await Form(c,"/Yonetim/Gorevler",bad)).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Form(c,"/Yonetim/Gorevler",Gorev(2,9))).StatusCode);
        using var scope=app.Services.CreateScope();Assert.False(await scope.ServiceProvider.GetRequiredService<UygulamaDbContext>().Gorevler.AnyAsync(g=>g.No==9));
    }

    [Fact]
    public async Task EksikGecmisGercekKayitGibiGosterilmez()
    {
        using var app=new IdentityApplication();using var c=await app.IstemciAsync();
        Assert.Equal(HttpStatusCode.Redirect,(await c.GetAsync("/Gonderimler")).StatusCode);
        await Giris(app,c,"ogrenci@example.test");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await c.GetAsync("/Gonderimler")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await c.GetAsync("/Gonderimler/1")).StatusCode);
    }

    private sealed class Gecmis : IGonderimGecmisi
    {
        public bool Kullanilabilir=>true;public List<GonderimGorunumu> Items=[]; public bool YanlisSahip;
        public Task<GonderimSayfasi> ListeAsync(string user,int page,int size,CancellationToken ct){var rows=Items.Where(g=>g.KullaniciId==user).ToArray();return Task.FromResult(new GonderimSayfasi(rows.Length,rows.Skip((page-1)*size).Take(size).ToArray()));}
        public Task<GonderimGorunumu?> DetayAsync(string user,int id,CancellationToken ct)=>Task.FromResult(Items.FirstOrDefault(g=>g.Id==id&&(YanlisSahip||g.KullaniciId==user)));
    }
    [Fact]
    public async Task GecmisKullaniciyaOzelSayfalanirVeDigerSahip404Alir()
    {
        var history=new Gecmis(); using var basis=new IdentityApplication();
        using var app=basis.WithWebHostBuilder(b=>b.ConfigureServices(s=>s.AddSingleton<IGonderimGecmisi>(history)));
        using var c=app.CreateClient(new(){AllowAutoRedirect=false,BaseAddress=new Uri("https://localhost")});
        using(var scope=app.Services.CreateScope())await scope.ServiceProvider.GetRequiredService<UygulamaDbContext>().Database.EnsureCreatedAsync();
        await Giris(app,c,"bir@example.test"); string user;
        using(var scope=app.Services.CreateScope()) user=(await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync("bir@example.test"))!.Id;
        for(int n=1;n<=21;n++) history.Items.Add(new(n,user,DateTimeOffset.UtcNow,1,"Kendi dersim", "Görev 1", "basarili", "<script>kod</script>", "<b>çıktı</b>", ""));
        history.Items.Add(new(99,"baskasi",DateTimeOffset.UtcNow,1,"GİZLİ BAŞKA DERS",null,"hata","gizli","",""));
        var html=await c.GetStringAsync("/Gonderimler");Assert.DoesNotContain("GİZLİ",html);Assert.Contains("Sonraki",html);
        html=WebUtility.HtmlDecode(await c.GetStringAsync("/Gonderimler?Sayfa=2"));Assert.Contains("Gönderim #21",html);Assert.DoesNotContain("Gönderim #20",html);
        var detail=await c.GetStringAsync("/Gonderimler/1");Assert.DoesNotContain("<script>kod</script>",detail);Assert.Contains("&lt;script&gt;",detail);
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync("/Gonderimler/99")).StatusCode);
        history.YanlisSahip=true;Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync("/Gonderimler/99")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync("/Gonderimler?Sayfa=0")).StatusCode);
        history.Items.Clear();Assert.Contains("Henüz gönderimin yok",WebUtility.HtmlDecode(await c.GetStringAsync("/Gonderimler")));
    }
}
