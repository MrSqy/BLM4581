using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EgitimPlatformu.Web.Tests;

public sealed class NotebookApiTests
{
    private const string Yol = "/api/notebooklar";
    private static readonly JsonElement Belge = JsonSerializer.Deserialize<JsonElement>("""
        {
          "nbformat": 4, "nbformat_minor": 5, "metadata": {"ozel": true},
          "cells": [{"id":"giris","cell_type":"code","source":["print(42)\n"],
             "metadata":{"etiket":"deneme"},"outputs":[{"output_type":"stream","name":"stdout","text":"42\n"}],
             "execution_count":1}],"ekAlan":{"koru":true}
        }
        """);

    [Fact]
    public async Task KisiselNotebookYalnizSahibineAciktirVeEskiSurumYazilamaz()
    {
        using var app = new IdentityApplication();
        using var sahip = await app.IstemciAsync();
        using var baska = await app.IstemciAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await sahip.GetAsync(Yol)).StatusCode);
        await OturumAc(app, sahip, "bir@example.test");
        await OturumAc(app, baska, "iki@example.test", yonetici: true);

        using var olustur = await sahip.PostAsJsonAsync(Yol, new { baslik = "  Kişisel  ", icerik = Belge });
        Assert.Equal(HttpStatusCode.Created, olustur.StatusCode);
        var ilk = (await olustur.Content.ReadFromJsonAsync<NotebookDetayi>())!;
        var adres = $"{Yol}/{ilk.Id}";
        Assert.Equal("Kişisel", ilk.Baslik);
        Assert.Equal(1, ilk.Surum);
        Assert.Equal(ilk.Id, Assert.Single((await sahip.GetFromJsonAsync<NotebookOzeti[]>(Yol + "?kisisel=true"))!).Id);
        Assert.Empty((await baska.GetFromJsonAsync<NotebookOzeti[]>(Yol))!);
        Assert.Equal(HttpStatusCode.NotFound, (await baska.GetAsync(adres)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await baska.PutAsJsonAsync(adres, new { baslik = "Gizli", icerik = Belge, surum = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await baska.DeleteAsync(adres)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await sahip.PutAsJsonAsync(adres, new { baslik = "Güncel", icerik = Belge, surum = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await sahip.PutAsJsonAsync(adres, new { baslik = "Eski", icerik = Belge, surum = 1 })).StatusCode);
        var guncel = (await sahip.GetFromJsonAsync<NotebookDetayi>(adres))!;
        Assert.Equal("Güncel", guncel.Baslik);
        Assert.Equal(2, guncel.Surum);
        Assert.True(JsonElement.DeepEquals(Belge, guncel.Icerik));
        Assert.Equal(HttpStatusCode.NoContent, (await sahip.DeleteAsync(adres)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sahip.GetAsync(adres)).StatusCode);
    }

    [Fact]
    public async Task DersNotebookunuYoneticiBaglarOgrenciOkur()
    {
        using var app = new IdentityApplication();
        using var ogrenci = await app.IstemciAsync();
        using var yonetici = await app.IstemciAsync();
        await OturumAc(app, ogrenci, "ogrenci@example.test");
        await OturumAc(app, yonetici, "yonetici@example.test", yonetici: true);

        var istek = new { baslik = "Ders örneği", icerik = Belge, egitimId = 1 };
        Assert.Equal(HttpStatusCode.Forbidden, (await ogrenci.PostAsJsonAsync(Yol, istek)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await yonetici.PostAsJsonAsync(Yol, new { baslik = "Bozuk", icerik = new { nbformat = "4" } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await yonetici.PostAsJsonAsync(Yol, istek with { egitimId = int.MaxValue })).StatusCode);
        using var olustur = await yonetici.PostAsJsonAsync(Yol, istek);
        Assert.Equal(HttpStatusCode.Created, olustur.StatusCode);
        var notebook = (await olustur.Content.ReadFromJsonAsync<NotebookDetayi>())!;
        var adres = $"{Yol}/{notebook.Id}";
        Assert.Equal(notebook.Id, Assert.Single((await ogrenci.GetFromJsonAsync<NotebookOzeti[]>(Yol + "?egitimId=1"))!).Id);
        Assert.Empty((await ogrenci.GetFromJsonAsync<NotebookOzeti[]>(Yol + "?egitimId=2"))!);
        Assert.True(JsonElement.DeepEquals(Belge, (await ogrenci.GetFromJsonAsync<NotebookDetayi>(adres))!.Icerik));
        Assert.Equal(HttpStatusCode.NotFound, (await ogrenci.DeleteAsync(adres)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await yonetici.DeleteAsync(adres)).StatusCode);
    }

    [Fact]
    public async Task YoneticiDosyayiDerseBaglarHucreleriDuzenlerVeOgrenciSonucuOkur()
    {
        using var app = new IdentityApplication();
        using var yonetici = await app.IstemciAsync();
        using var ogrenci = await app.IstemciAsync();
        const string panel = "/Yonetim/Notebooklar";
        await OturumAc(app, yonetici, "yonetici@example.test", yonetici: true);
        Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.GirisAsync(yonetici, "yonetici@example.test")).StatusCode);
        await OturumAc(app, ogrenci, "ogrenci@example.test");
        Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.GirisAsync(ogrenci)).StatusCode);
        var html = await yonetici.GetStringAsync(panel);
        var csrf = Regex.Match(html, """name="__RequestVerificationToken"[^>]*value="([^"]+)" """.TrimEnd()).Groups[1].Value;
        Assert.NotEmpty(csrf);
        using var form = new MultipartFormDataContent
        {
            { new StringContent(csrf), "__RequestVerificationToken" },
            { new StringContent("Notebook sınaması"), "Baslik" },
            { new StringContent("1"), "EgitimId" },
            { new StringContent(Belge.GetRawText(), Encoding.UTF8, "application/json"), "Belge", "ders.ipynb" }
        };
        using var olustur = await yonetici.PostAsync(panel, form);
        Assert.Equal(HttpStatusCode.Redirect, olustur.StatusCode);
        var adres = olustur.Headers.Location!.OriginalString;
        Assert.StartsWith("/Notebooklar/Detay/", adres);
        Assert.Contains(adres, await ogrenci.GetStringAsync("/Egitimler/Detay/1"));
        var yoneticiHtml = await yonetici.GetStringAsync(adres);
        Assert.Contains("Kod hücresi ekle", WebUtility.HtmlDecode(yoneticiHtml));
        Assert.DoesNotContain("name=\"islem\"", await ogrenci.GetStringAsync(adres));

        async Task Islem(string islem, int surum, int sira = 0, string kaynak = "")
        {
            using var yanit = await IdentityApplication.FormAsync(yonetici, adres + "?handler=Hucre", new()
            {
                ["islem"] = islem, ["surum"] = surum.ToString(), ["sira"] = sira.ToString(), ["kaynak"] = kaynak
            });
            Assert.Equal(HttpStatusCode.Redirect, yanit.StatusCode);
        }
        await Islem("markdown", 1);
        await Islem("kaydet", 2, 1, "# Hafta 6\n\n<script>alert(1)</script>");
        await Islem("yukari", 3, 1);
        await Islem("kod", 4);
        await Islem("sil", 5, 2);
        await Islem("sil", 5, 0); // Eski sürümdeki gönderim içeriği değiştirmez.

        var icerik = (await ogrenci.GetFromJsonAsync<NotebookDetayi>(Yol + "/" + adres.Split('/').Last()))!;
        Assert.Equal(6, icerik.Surum);
        var hucreler = icerik.Icerik.GetProperty("cells");
        Assert.Equal(2, hucreler.GetArrayLength());
        Assert.Equal("markdown", hucreler[0].GetProperty("cell_type").GetString());
        Assert.True(JsonElement.DeepEquals(Belge.GetProperty("cells")[0], hucreler[1]));
        Assert.True(icerik.Icerik.GetProperty("ekAlan").GetProperty("koru").GetBoolean());
        var ogrenciHtml = await ogrenci.GetStringAsync(adres);
        Assert.Contains(">Hafta 6</h1>", ogrenciHtml);
        Assert.DoesNotContain("<script>alert(1)</script>", ogrenciHtml);
        Assert.Contains("Kayıtlı çıktılar", WebUtility.HtmlDecode(ogrenciHtml));

        using var sil = await IdentityApplication.FormAsync(yonetici, panel + $"?handler=Sil&id={icerik.Id}", new() { ["Onay"] = "true" });
        Assert.Equal(HttpStatusCode.Redirect, sil.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ogrenci.GetAsync(adres)).StatusCode);
    }

    [Fact]
    public async Task MobilWebSocketGuvenliNotebookSecimiVePingPong()
    {
        using var app = new IdentityApplication();
        using var sahip = await app.IstemciAsync();
        using var baska = await app.IstemciAsync();
        const string yol = "/api/mobil/notebook-oturumu";
        Assert.Equal(HttpStatusCode.Unauthorized, (await sahip.GetAsync(yol)).StatusCode);
        await OturumAc(app, sahip, "sahip@example.test");
        await OturumAc(app, baska, "baska@example.test");
        Assert.Equal(HttpStatusCode.UpgradeRequired, (await sahip.GetAsync(yol)).StatusCode);
        using var olustur = await sahip.PostAsJsonAsync(Yol, new { baslik = "Kişisel", icerik = Belge });
        var notebook = (await olustur.Content.ReadFromJsonAsync<NotebookDetayi>())!;

        using var sure = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var uri = new Uri($"wss://localhost{yol}?notebookId={notebook.Id}");
        var istemci = app.Server.CreateWebSocketClient();
        var diger = app.Server.CreateWebSocketClient();
        diger.ConfigureRequest = istek => istek.Headers.Authorization = baska.DefaultRequestHeaders.Authorization!.ToString();
        await Assert.ThrowsAsync<InvalidOperationException>(() => diger.ConnectAsync(uri, sure.Token));

        istemci.ConfigureRequest = istek => istek.Headers.Authorization = sahip.DefaultRequestHeaders.Authorization!.ToString();
        using var soket = await istemci.ConnectAsync(uri, sure.Token);
        var tampon = new byte[1024];
        async Task<JsonElement> Oku()
        {
            var yanit = await soket.ReceiveAsync(tampon.AsMemory(), sure.Token);
            Assert.True(yanit.EndOfMessage);
            return JsonSerializer.Deserialize<JsonElement>(tampon.AsSpan(0, yanit.Count));
        }
        var ilk = await Oku();
        Assert.Equal("baglandi", ilk.GetProperty("tur").GetString());
        Assert.Equal(notebook.Id, ilk.GetProperty("notebookId").GetInt32());
        var id = ilk.GetProperty("oturumId").GetGuid();
        Assert.NotEqual(Guid.Empty, id);
        await soket.SendAsync("""{"tur":"ping"}"""u8.ToArray().AsMemory(), WebSocketMessageType.Text, true, sure.Token);
        var pong = await Oku();
        Assert.Equal("pong", pong.GetProperty("tur").GetString());
        Assert.Equal(id, pong.GetProperty("oturumId").GetGuid());
        await soket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", sure.Token);
    }

    [Fact]
    public async Task MobilYonetimDersVeGorevCrudRoluVePasifeAlmayiKorur()
    {
        using var app = new IdentityApplication();
        using var yonetici = await app.IstemciAsync();
        using var ogrenci = await app.IstemciAsync();
        const string kok = "/api/mobil/yonetim/egitimler";
        await OturumAc(app, yonetici, "yonetici@example.test", yonetici: true);
        await OturumAc(app, ogrenci, "ogrenci@example.test");
        var ders = new
        {
            kategoriId = 1, kod = "HAFTA-06", baslik = "Yerel ders", sira = 99,
            icerikMarkdown = "# Ders", baslangicKodu = "pass", testKodu = "assert True"
        };
        Assert.Equal(HttpStatusCode.Forbidden, (await ogrenci.PostAsJsonAsync(kok, ders)).StatusCode);
        using var olustur = await yonetici.PostAsJsonAsync(kok, ders);
        Assert.Equal(HttpStatusCode.Created, olustur.StatusCode);
        var id = (await olustur.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var adres = $"{kok}/{id}";
        Assert.Equal(HttpStatusCode.Forbidden, (await ogrenci.GetAsync(adres)).StatusCode);
        Assert.Equal(ders.testKodu, (await yonetici.GetFromJsonAsync<JsonElement>(adres)).GetProperty("testKodu").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await yonetici.PutAsJsonAsync(adres, ders with { baslik = "Güncel ders" })).StatusCode);
        Assert.Equal("Güncel ders", (await yonetici.GetFromJsonAsync<JsonElement>(adres)).GetProperty("baslik").GetString());
        var gorevler = adres + "/gorevler";
        var gorev = new { egitimId = id, no = 1, baslik = "Görev", tur = "aciklama", aktif = true };
        Assert.Equal(HttpStatusCode.Forbidden, (await ogrenci.PostAsJsonAsync(gorevler, gorev)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await yonetici.PostAsJsonAsync(gorevler, gorev with { egitimId = 1 })).StatusCode);
        using var gorevYanit = await yonetici.PostAsJsonAsync(gorevler, gorev);
        Assert.Equal(HttpStatusCode.Created, gorevYanit.StatusCode);
        var gorevId = (await gorevYanit.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await yonetici.PostAsJsonAsync(gorevler, gorev)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await yonetici.PutAsJsonAsync($"{gorevler}/{gorevId}", gorev with { baslik = "Güncel görev" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await yonetici.DeleteAsync($"{gorevler}/{gorevId}")).StatusCode);
        var kayit = Assert.Single((await yonetici.GetFromJsonAsync<JsonElement[]>(gorevler))!);
        Assert.False(kayit.GetProperty("aktif").GetBoolean());
        Assert.Equal("Güncel görev", kayit.GetProperty("baslik").GetString());
        Assert.Empty((await ogrenci.GetFromJsonAsync<JsonElement[]>($"/api/egitimler/{id}/gorevler"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await yonetici.DeleteAsync(adres)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await yonetici.GetAsync(adres)).StatusCode);
    }

    private static async Task OturumAc(IdentityApplication app, HttpClient istemci, string eposta, bool yonetici = false)
    {
        Assert.Equal(HttpStatusCode.Redirect, (await IdentityApplication.KayitAsync(istemci, eposta)).StatusCode);
        if (yonetici)
        {
            using var scope = app.Services.CreateScope();
            var kullanicilar = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var kullanici = (await kullanicilar.FindByEmailAsync(eposta))!;
            Assert.True((await kullanicilar.AddToRoleAsync(kullanici, Roller.Yonetici)).Succeeded);
        }
        using var yanit = await istemci.PostAsJsonAsync("/api/hesap/giris", new { eposta, sifre = "Guvenli123!" });
        yanit.EnsureSuccessStatusCode();
        var json = await yanit.Content.ReadFromJsonAsync<JsonElement>();
        istemci.DefaultRequestHeaders.Authorization = new("Bearer", json.GetProperty("accessToken").GetString());
    }
}
