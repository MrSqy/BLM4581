namespace EgitimPlatformu.Web.Services;

public sealed record KodCalistirmaSonucu(int CikisKodu, string StandartCikti, string StandartHata)
{
    public bool Basarili => CikisKodu == 0;
    public bool ZamanAsimi => CikisKodu == 124;
    public bool AltyapiHatasi => CikisKodu == 125;
}

public sealed record SandboxCalistirmaIstegi(string CalismaDizini, int SureSiniriSaniye);

public interface IKodCalistirmaServisi
{
    Task<KodCalistirmaSonucu> CalistirAsync(string kod, string testKodu,
        CancellationToken cancellationToken = default);
}

public interface ISandboxIslemCalistirici
{
    Task<KodCalistirmaSonucu> CalistirAsync(SandboxCalistirmaIstegi istek,
        CancellationToken cancellationToken = default);
}

public sealed class PythonKodCalistirmaServisi(ISandboxIslemCalistirici sandbox) : IKodCalistirmaServisi
{
    private const int SureSiniriSaniye = 5;

    public async Task<KodCalistirmaSonucu> CalistirAsync(string kod, string testKodu,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kod);
        ArgumentException.ThrowIfNullOrWhiteSpace(testKodu);

        var calismaDizini = Path.Combine(Path.GetTempPath(), $"egitim-kod-{Guid.NewGuid():N}");
        Directory.CreateDirectory(calismaDizini);
        try
        {
            await File.WriteAllTextAsync(Path.Combine(calismaDizini, "calisma.py"), kod, cancellationToken);
            await File.WriteAllTextAsync(Path.Combine(calismaDizini, "test_alistirmalar.py"), testKodu,
                cancellationToken);
            return await sandbox.CalistirAsync(
                new SandboxCalistirmaIstegi(calismaDizini, SureSiniriSaniye), cancellationToken);
        }
        finally
        {
            if (Directory.Exists(calismaDizini)) Directory.Delete(calismaDizini, recursive: true);
        }
    }
}
