using System.Text.Json;

namespace EgitimPlatformu.Web.Services;

public sealed record NotebookHucreSonucu(string Durum, JsonElement[] Ciktilar);
public sealed record NotebookCalistirmaSonucu(string Durum, NotebookHucreSonucu[] Hucreler);

public interface INotebookCalistirmaServisi
{
    Task<NotebookCalistirmaSonucu> CalistirAsync(IReadOnlyList<string> hucreler,
        CancellationToken cancellationToken = default);
}

public sealed class NotebookCalistirmaServisi(IHariciIslemCalistirici islem)
    : INotebookCalistirmaServisi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<NotebookCalistirmaSonucu> CalistirAsync(IReadOnlyList<string> hucreler,
        CancellationToken cancellationToken = default)
    {
        if (hucreler.Count is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(hucreler));

        var veri = JsonSerializer.SerializeToUtf8Bytes(hucreler);
        if (veri.Length > 256 * 1024)
            throw new ArgumentException("Notebook kodu boyut sınırını aşıyor.", nameof(hucreler));

        var dizin = Directory.CreateTempSubdirectory("egitim-notebook-");
        try
        {
            var yol = Path.Combine(dizin.FullName, "istek.json");
            await File.WriteAllBytesAsync(yol, veri, cancellationToken);
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(yol, UnixFileMode.UserRead | UnixFileMode.OtherRead);
            var betik = Path.Combine(AppContext.BaseDirectory, "scripts", "run-python-sandbox.sh");
            var sonuc = await islem.CalistirAsync("setsid",
                ["bash", betik, "--jupyter", "--timeout", "20", "--volume", $"{yol}:/istek.json:ro",
                    "--", "python", "/opt/notebook/calistir.py"], cancellationToken);

            if (sonuc.CikisKodu == 124) return new("zaman_asimi", []);
            if (sonuc.CikisKodu != 0) return new("altyapi_yok", []);

            try
            {
                var yanit = JsonSerializer.Deserialize<NotebookCalistirmaSonucu>(sonuc.StandartCikti, Json);
                if (yanit is { Durum: "tamamlandi" or "cikti_siniri", Hucreler: not null })
                    return yanit;
            }
            catch (JsonException)
            {
                // Geçersiz çalışma çıktısı bir hücre sonucu olarak sunulmaz.
            }
            return new("altyapi_yok", []);
        }
        finally
        {
            dizin.Delete(recursive: true);
        }
    }
}
