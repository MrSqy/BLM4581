using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;

namespace EgitimPlatformu.Web.Contracts;

public sealed class GorevFormu : IValidatableObject
{
    [Required(ErrorMessage = "Eğitim seçin."), Range(1, int.MaxValue, ErrorMessage = "Geçerli eğitim seçin."), Display(Name = "Eğitim")] public int? EgitimId { get; set; }
    [Required(ErrorMessage = "Görev numarası zorunludur."), Range(1, 100, ErrorMessage = "Görev numarası 1–100 arasında olmalı."), Display(Name = "Görev numarası")] public int? No { get; set; } = 1;
    [Required(ErrorMessage = "Başlık zorunludur."), StringLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir."), Display(Name = "Başlık")] public string Baslik { get; set; } = "";
    [StringLength(50000, ErrorMessage = "Açıklama en fazla 50000 karakter olabilir."), Display(Name = "Açıklama (Markdown)")] public string? Aciklama { get; set; } = "";
    [Required(ErrorMessage = "Görev türü seçin."), RegularExpression("kod|aciklama|eski-pytest", ErrorMessage = "Geçerli görev türü seçin."), Display(Name = "Görev türü")] public string Tur { get; set; } = "kod";
    [Display(Name = "Aktif")] public bool Aktif { get; set; } = true;
    [StringLength(200, ErrorMessage = "Dosya adı çok uzun."), Display(Name = "Başlangıç dosyası (gorevler/ad.py)")] public string? BaslangicDosyasi { get; set; }
    [StringLength(100000, ErrorMessage = "Kod en fazla 100000 karakter olabilir."), Display(Name = "Başlangıç kodu")] public string? BaslangicKodu { get; set; } = "";
    [StringLength(64000, ErrorMessage = "Beklenen çıktı en fazla 64000 karakter olabilir."), Display(Name = "Beklenen çıktı")] public string? BeklenenCikti { get; set; } = "";
    [StringLength(32000, ErrorMessage = "Girdi en fazla 32000 karakter olabilir."), Display(Name = "Standart girdi")] public string? Girdi { get; set; } = "";
    [Range(0, 255, ErrorMessage = "Çıkış kodu 0–255 arasında olmalı."), Display(Name = "Beklenen çıkış kodu")] public int BeklenenCikisKodu { get; set; }
    [StringLength(3100, ErrorMessage = "En fazla 30 kural kullanın."), Display(Name = "Kullanılmaması gereken ifadeler (satır başına bir)")] public string? Yasaklar { get; set; } = "";
    public IEnumerable<ValidationResult> Cakismalar(IEnumerable<Gorev> kayitlar, int? duzenleId)
    {
        if (kayitlar.Any(g => g.EgitimId == EgitimId && g.No == No && g.Id != duzenleId)) yield return new("Bu eğitimde aynı görev numarası kullanılıyor (pasif görevler dahil).", [nameof(No)]);
        if (Tur == "kod" && kayitlar.Any(g => g.EgitimId == EgitimId && g.Id != duzenleId && g.Tur == "kod" && g.BaslangicDosyasi == BaslangicDosyasi)) yield return new("Bu başlangıç dosyası eğitimde zaten kullanılıyor.", [nameof(BaslangicDosyasi)]);
        if (Aktif && kayitlar.Any(g => g.EgitimId == EgitimId && g.Id != duzenleId && g.Aktif && (g.Tur == "eski-pytest") != (Tur == "eski-pytest"))) yield return new("Aynı eğitimde aktif eski-pytest ve görev bazlı içerik karıştırılamaz.", [nameof(Tur)]);
    }

    public void Uygula(Gorev entity)
    {
        entity.EgitimId = EgitimId; entity.No = No; entity.Baslik = Baslik; entity.Aciklama = Aciklama ?? "";
        entity.Aktif = Aktif; entity.Tur = Tur; entity.BaslangicKodu = BaslangicKodu ?? "";
        entity.BaslangicDosyasi = string.IsNullOrEmpty(BaslangicDosyasi) ? null : BaslangicDosyasi;
        GorevKontrolu? eskiKontrol = null;
        try { if (entity.KontrolJson != null) eskiKontrol = JsonSerializer.Deserialize<GorevKontrolu>(entity.KontrolJson); } catch (JsonException) { }
        var kontrol = Kontrol();
        // HTML formları satır sonlarını CRLF gönderir. Aynı metinde özgün değer korunur;
        // yeni metinler Python'un standart LF çıktısına göre kaydedilir, boşluklar kırpılmaz.
        static string Metin(string value, string? original) => original != null && value.Replace("\r\n", "\n") == original.Replace("\r\n", "\n") ? original : value.Replace("\r\n", "\n");
        kontrol.BeklenenCikti = Metin(kontrol.BeklenenCikti, eskiKontrol?.BeklenenCikti);
        kontrol.Girdi = Metin(kontrol.Girdi, eskiKontrol?.Girdi);
        entity.KontrolJson = Tur == "kod" ? JsonSerializer.Serialize(kontrol) : null;
    }

    public GorevKontrolu Kontrol() => new() { Tur = "standart-cikti", BeklenenCikti = BeklenenCikti ?? "", Girdi = Girdi ?? "", BeklenenCikisKodu = BeklenenCikisKodu, Kullanilmamali = (Yasaklar ?? "").Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList() };
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var k = Kontrol();
        if (k.Kullanilmamali.Count > 30) yield return new("En fazla 30 kural kullanın.", [nameof(Yasaklar)]);
        if (Tur != "kod" && (!string.IsNullOrEmpty(BaslangicKodu) || !string.IsNullOrEmpty(BaslangicDosyasi) || !string.IsNullOrEmpty(BeklenenCikti) || !string.IsNullOrEmpty(Girdi) || !string.IsNullOrEmpty(Yasaklar) || BeklenenCikisKodu != 0))
            yield return new("Açıklama/eski-pytest türünde görev başlangıç ve kontrol alanlarını boş bırakın.");
        if (Tur == "eski-pytest") yield break;
        // V2 içerik aktarımının mevcut güvenli dosya/tür/kural doğrulaması yeniden kullanılır.
        var paket = new IcerikPaketi { Surum = 2, Kategoriler = [new() { Kisaltma = "K", Egitimler = [new() { Kod = "D", Gorevler = [new() { No = No ?? 1, Tur = Tur, BaslangicKodu = BaslangicKodu ?? "", BaslangicDosyasi = string.IsNullOrEmpty(BaslangicDosyasi) ? null : BaslangicDosyasi, Kontrol = Tur == "kod" ? k : null }] }] }] };
        var hata = IcerikAktarimServisi.TutarlilikHatasi(paket);
        if (hata != null) yield return new(hata);
    }
}
