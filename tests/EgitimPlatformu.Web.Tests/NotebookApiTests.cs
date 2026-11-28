using System.Net;
using System.Net.Http.Json;
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
