using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using EgitimPlatformu.Web.Contracts;
using EgitimPlatformu.Web.Data;
using EgitimPlatformu.Web.Models;
using EgitimPlatformu.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace EgitimPlatformu.Web.Pages.Yonetim;

public class GorevlerModel(UygulamaDbContext db) : YonetimPageModel(db)
{
    [BindProperty] public GorevFormu Input { get; set; } = new();
    public List<Gorev> Kayitlar { get; private set; } = [];
    public List<Egitim> Egitimler { get; private set; } = [];
    private async Task Yukle(CancellationToken ct)
    {
        Kayitlar = await Db.Gorevler.AsNoTracking().Include(g => g.Egitim).OrderBy(g => g.EgitimId).ThenBy(g => g.No).ToListAsync(ct);
        Egitimler = await Db.Egitimler.AsNoTracking().OrderBy(e => e.Kod).ToListAsync(ct);
    }
    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        await Yukle(ct);
        if (DuzenleId.HasValue)
        {
            var g = Kayitlar.Find(g => g.Id == DuzenleId); if (g is null) return NotFound();
            GorevKontrolu? k = null;
            try { if (g.KontrolJson != null) k = JsonSerializer.Deserialize<GorevKontrolu>(g.KontrolJson); }
            catch (JsonException) { ModelState.AddModelError("", "Mevcut kontrol verisi okunamadı. Kaydetmeden önce ölçütleri kontrol edin."); }
            Input = new() { EgitimId = g.EgitimId, No = g.No, Baslik = g.Baslik, Aciklama = g.Aciklama, Tur = g.Tur, Aktif = g.Aktif, BaslangicDosyasi = g.BaslangicDosyasi, BaslangicKodu = g.BaslangicKodu, BeklenenCikti = k?.BeklenenCikti, Girdi = k?.Girdi, BeklenenCikisKodu = k?.BeklenenCikisKodu ?? 0, Yasaklar = k == null ? "" : string.Join("\n", k.Kullanilmamali) };
        }
        if (SilId.HasValue) { var g = Kayitlar.Find(g => g.Id == SilId); if (g is null) return NotFound(); SilinecekAd = g.Baslik; }
        return Page();
    }
    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        await Yukle(ct); if (!ModelState.IsValid) return Page();
        if (!Egitimler.Any(e => e.Id == Input.EgitimId)) ModelState.AddModelError("Input.EgitimId", "Eğitim bulunamadı.");
        if (Kayitlar.Any(g => g.EgitimId == Input.EgitimId && g.No == Input.No && g.Id != DuzenleId)) ModelState.AddModelError("Input.No", "Bu eğitimde aynı görev numarası kullanılıyor (pasif görevler dahil).");
        if (Input.Tur == "kod" && Kayitlar.Any(g => g.EgitimId == Input.EgitimId && g.Id != DuzenleId && g.Tur == "kod" && g.BaslangicDosyasi == Input.BaslangicDosyasi)) ModelState.AddModelError("Input.BaslangicDosyasi", "Bu başlangıç dosyası eğitimde zaten kullanılıyor.");
        if (Input.Aktif && Kayitlar.Any(g => g.EgitimId == Input.EgitimId && g.Id != DuzenleId && g.Aktif && (g.Tur == "eski-pytest") != (Input.Tur == "eski-pytest"))) ModelState.AddModelError("Input.Tur", "Aynı eğitimde aktif eski-pytest ve görev bazlı içerik karıştırılamaz.");
        if (!ModelState.IsValid) return Page();
        var entity = DuzenleId.HasValue ? await Db.Gorevler.FindAsync([DuzenleId.Value], ct) : new Gorev();
        if (entity is null) return NotFound();
        entity.EgitimId = Input.EgitimId; entity.No = Input.No; entity.Baslik = Input.Baslik; entity.Aciklama = Input.Aciklama ?? "";
        entity.Aktif = Input.Aktif; entity.Tur = Input.Tur; entity.BaslangicKodu = Input.BaslangicKodu ?? "";
        entity.BaslangicDosyasi = string.IsNullOrEmpty(Input.BaslangicDosyasi) ? null : Input.BaslangicDosyasi;
        GorevKontrolu? eskiKontrol = null;
        try { if (entity.KontrolJson != null) eskiKontrol = JsonSerializer.Deserialize<GorevKontrolu>(entity.KontrolJson); } catch (JsonException) { }
        var kontrol = Input.Kontrol();
        // HTML formları satır sonlarını CRLF gönderir. Aynı metinde özgün değer korunur;
        // yeni metinler Python'un standart LF çıktısına göre kaydedilir, boşluklar kırpılmaz.
        static string Metin(string value, string? original) => original != null && value.Replace("\r\n", "\n") == original.Replace("\r\n", "\n") ? original : value.Replace("\r\n", "\n");
        kontrol.BeklenenCikti = Metin(kontrol.BeklenenCikti, eskiKontrol?.BeklenenCikti);
        kontrol.Girdi = Metin(kontrol.Girdi, eskiKontrol?.Girdi);
        entity.KontrolJson = Input.Tur == "kod" ? JsonSerializer.Serialize(kontrol) : null;
        if (!DuzenleId.HasValue) Db.Gorevler.Add(entity);
        if (!await Kaydet(ct)) return Page();
        TempData["Bildirim"] = "Görev kaydedildi."; return RedirectToPage();
    }
    public async Task<IActionResult> OnPostSilAsync(int id, CancellationToken ct)
    {
        ModelState.Clear(); await Yukle(ct); var g = await Db.Gorevler.FindAsync([id], ct); if (g is null) return NotFound();
        SilId = id; SilinecekAd = g.Baslik; if (!SilmeOnayi()) return Page();
        Db.Gorevler.Remove(g); if (!await Kaydet(ct)) return Page();
        TempData["Bildirim"] = "Görev silindi."; return RedirectToPage();
    }
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
}
