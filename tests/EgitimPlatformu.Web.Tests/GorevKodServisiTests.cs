using System.Text.Json;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Xunit;
namespace EgitimPlatformu.Web.Tests;

public class GorevKodServisiTests
{
    private sealed class Process(int exit, string output) : IHariciIslemCalistirici
    {
        public string? Dir;
        public Task<KodCalistirmaSonucu> CalistirAsync(string file, IReadOnlyList<string> args, CancellationToken cancellationToken)
        {
            Assert.Equal("setsid", file);
            Dir = args.Single(x => x.EndsWith(":/calisma:ro"))[..^":/calisma:ro".Length];
            var request = File.ReadAllText(Path.Combine(Dir,"istek.json"));
            Assert.DoesNotContain("SUNUCU_BEKLENEN",request); Assert.DoesNotContain("beklenenCikisKodu",request);
            Assert.Equal("print('deneme')",File.ReadAllText(Path.Combine(Dir,"gorevler/selam.py")));
            return Task.FromResult(new KodCalistirmaSonucu(exit,output,""));
        }
    }
    private static Gorev Gorev() => new() { No=1, Tur="kod", BaslangicDosyasi="gorevler/selam.py", KontrolJson=JsonSerializer.Serialize(new GorevKontrolu { Tur="standart-cikti", BeklenenCikti="SUNUCU_BEKLENEN\n", Girdi="girdi\n",BeklenenCikisKodu=3,Kullanilmamali=[] }) };
    [Theory]
    [InlineData("SUNUCU_BEKLENEN\n",3,false,true)]
    [InlineData("SUNUCU_BEKLENEN",3,false,false)]
    [InlineData("SUNUCU_BEKLENEN\n",0,false,false)]
    [InlineData("SUNUCU_BEKLENEN\n",3,true,false)]
    public async Task CiktiCikisVeKurallarSunucudaKarsilastirilir(string output,int exit,bool rule,bool expected)
    {
        var data=JsonSerializer.Serialize(new { cikti=output,hata="",cikisKodu=exit,sinirAsildi=false,kuralIhlalleri=rule?new[]{"eval"}:[] });
        var process=new Process(0,data);
        var result=await new GorevKodServisi(process).CalistirAsync(Gorev(),"print('deneme')",true,default);
        Assert.Equal(expected,result.Basarili);Assert.Equal(output,result.Cikti);Assert.Equal(exit,result.CikisKodu);
        Assert.False(Directory.Exists(process.Dir));
    }
    [Theory]
    [InlineData(124)][InlineData(125)][InlineData(1)]
    public async Task AltyapiHatasiVeZamanAsimiBasariDegildir(int exit)
    {
        var process=new Process(exit,"");var service=new GorevKodServisi(process);
        if(exit==124) Assert.Equal("zaman_asimi",(await service.CalistirAsync(Gorev(),"print('deneme')",true,default)).Durum);
        else await Assert.ThrowsAsync<InvalidOperationException>(()=>service.CalistirAsync(Gorev(),"print('deneme')",true,default));
        Assert.False(Directory.Exists(process.Dir));
    }
}
