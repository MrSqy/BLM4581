using System.Data.Common;

namespace EgitimPlatformu.Web.Services;

public static class GonderimGecmisiHatasi
{
    public static bool VeritabaniHatasiMi(Exception ex) =>
        ex is DbException or InvalidOperationException { InnerException: DbException };

    public const string OkumaMesaji = "Gönderim geçmişine şu anda ulaşılamıyor.";
    public const string KayitMesaji = "Test tamamlandı ancak gönderim kaydedilemedi.";
}
