using EgitimPlatformu.Web.Services;
using Xunit;
namespace EgitimPlatformu.Web.Tests;

public class MobilKodServisiTests
{
    private sealed class Process(KodCalistirmaSonucu result) : IHariciIslemCalistirici
    {
        public string? Dir;
        public Task<KodCalistirmaSonucu> CalistirAsync(string file, IReadOnlyList<string> args, CancellationToken cancellationToken)
        {
            Assert.Equal("setsid", file);
            var mount = args.Single(a => a.EndsWith(":/calisma:ro"));
            Dir = mount[..^":/calisma:ro".Length];
            Assert.Equal("pass", File.ReadAllText(Path.Combine(Dir, "calisma.py")));
            Assert.Equal("SERVER_TEST", File.ReadAllText(Path.Combine(Dir, "test_alistirmalar.py")));
            Assert.True(File.Exists(Path.Combine(Dir, "runner.py")));
            return Task.FromResult(result);
        }
    }
    [Fact]
    public async Task GorevSonuclariVeGeciciDosyaTemizligi()
    {
        var process = new Process(new(0, "MOBIL_SONUC={\"basarili\":false,\"durum\":\"basarisiz\",\"cikti\":\"\",\"hata\":\"\",\"gorevler\":{\"1\":\"basarili\",\"2\":\"basarisiz\"}}", ""));
        var result = await new MobilKodServisi(process).CalistirAsync("pass", "SERVER_TEST", true, [1, 2, 3], default);
        Assert.Equal(new[] { "basarili", "basarisiz", "test_yok" }, result.Gorevler.Select(g => g.Durum));
        Assert.False(Directory.Exists(process.Dir));
    }
    [Theory]
    [InlineData(124)][InlineData(125)][InlineData(1)]
    public async Task HataYollariBasariGibiSunulmaz(int code)
    {
        var process = new Process(new(code, "", ""));
        var service = new MobilKodServisi(process);
        if (code == 125) await Assert.ThrowsAsync<InvalidOperationException>(() => service.CalistirAsync("pass", "SERVER_TEST", true, [], default));
        else
        {
            var result = await service.CalistirAsync("pass", "SERVER_TEST", true, [], default);
            Assert.False(result.Basarili);
            Assert.Equal(code == 124 ? "zaman_asimi" : "hata", result.Durum);
        }
        Assert.False(Directory.Exists(process.Dir));
    }
}
