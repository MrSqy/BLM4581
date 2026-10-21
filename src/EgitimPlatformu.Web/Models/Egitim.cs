using System.ComponentModel.DataAnnotations;

namespace EgitimPlatformu.Web.Models;

// Eğitim deposundaki bir ders klasörü (ör. dersler/PY-03).
public class Egitim
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Kategori seçiniz.")]
    [Display(Name = "Kategori")]
    public int? KategoriId { get; set; }

    public Kategori? Kategori { get; set; }

    [Required(ErrorMessage = "Ders kodu zorunludur.")]
    [StringLength(20, ErrorMessage = "En fazla 20 karakter.")]
    [Display(Name = "Ders Kodu")]
    public string Kod { get; set; } = "";

    [Required(ErrorMessage = "Başlık zorunludur.")]
    [StringLength(150, ErrorMessage = "En fazla 150 karakter.")]
    [Display(Name = "Başlık")]
    public string Baslik { get; set; } = "";

    [Display(Name = "Sıra")]
    public int Sira { get; set; }

    // ALISTIRMALAR.md
    [Display(Name = "İçerik")]
    public string IcerikMarkdown { get; set; } = "";

    // calisma.py: öğrencinin editörde gördüğü başlangıç kodu.
    [Display(Name = "Başlangıç Kodu")]
    public string BaslangicKodu { get; set; } = "";

    // test_alistirmalar.py: test_gorev_N fonksiyonlarını içerir.
    [Display(Name = "Test Kodu")]
    public string TestKodu { get; set; } = "";

    public List<Gorev> Gorevler { get; set; } = new();
}
