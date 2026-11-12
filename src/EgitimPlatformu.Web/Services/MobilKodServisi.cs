using System.Text.Json;

namespace EgitimPlatformu.Web.Services;

public sealed record GorevTestSonucu(int No, string Durum);
public sealed record MobilKodSonucu(bool Basarili, string Durum, string Cikti, string Hata, IReadOnlyList<GorevTestSonucu> Gorevler)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? GonderimId { get; init; }
}
public interface IMobilKodServisi
{
    Task<MobilKodSonucu> CalistirAsync(string kod, string testKodu, bool test, int[] gorevler, CancellationToken ct);
}

public sealed class MobilKodServisi(IHariciIslemCalistirici process) : IMobilKodServisi
{
    public async Task<MobilKodSonucu> CalistirAsync(string kod, string testKodu, bool test, int[] gorevler, CancellationToken ct)
    {
        // Eşzamanlılık ve sıra ortak sandbox kuyruğunda (KuyrukluIslemCalistirici) tutulur.
        var dir = Path.Combine(Path.GetTempPath(), $"egitim-mobil-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            // Sandbox uid=10001 yalnız okuyabilir. Üst /tmp erişilebilirdir.
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(dir, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            await File.WriteAllTextAsync(Path.Combine(dir, "calisma.py"), kod, ct);
            await File.WriteAllTextAsync(Path.Combine(dir, "test_alistirmalar.py"), testKodu, ct);
            File.Copy(Path.Combine(AppContext.BaseDirectory, "scripts", "mobile-runner.py"), Path.Combine(dir, "runner.py"));
            foreach (var file in Directory.GetFiles(dir))
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            var result = await process.CalistirAsync("setsid", ["bash", Path.Combine(AppContext.BaseDirectory, "scripts", "run-python-sandbox.sh"),
                "--timeout", "8", "--volume", $"{dir}:/calisma:ro", "--workdir", "/calisma",
                "--env", "PYTHONDONTWRITEBYTECODE=1", "--env", "PYTEST_DISABLE_PLUGIN_AUTOLOAD=1",
                "--", "python", "-B", "runner.py", test ? "test" : "calistir"], ct);
            if (result.AltyapiHatasi) throw new InvalidOperationException("Sandbox altyapısı kullanılamıyor.");
            if (result.ZamanAsimi) return new(false, "zaman_asimi", "", "Kod süre sınırını aştı.", []);
            const string marker = "MOBIL_SONUC=";
            var line = result.StandartCikti.Split('\n').LastOrDefault(l => l.StartsWith(marker));
            if (line == null) return new(false, "hata", "", "Kod tamamlanamadı veya çıktı sınırını aştı.", []);
            using var json = JsonDocument.Parse(line[marker.Length..]);
            var data = json.RootElement;
            var statuses = data.GetProperty("gorevler");
            var tasks = test ? gorevler.Select(n => new GorevTestSonucu(n,
                statuses.TryGetProperty(n.ToString(), out var v) ? v.GetString()! : "test_yok")).ToArray() : [];
            return new(data.GetProperty("basarili").GetBoolean(), data.GetProperty("durum").GetString()!,
                data.GetProperty("cikti").GetString()!, data.GetProperty("hata").GetString()!, tasks);
        }
        catch (JsonException ex) { throw new InvalidOperationException("Sandbox yanıtı okunamadı.", ex); }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
