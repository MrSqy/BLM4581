using System.ComponentModel.DataAnnotations;

namespace EgitimPlatformu.Web.Models;

// Eğitim alanı: Python dili, Teknik Mülakat Algoritmalar vb.
public class Kategori
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Kategori adı zorunludur.")]
    [StringLength(100, ErrorMessage = "En fazla 100 karakter.")]
    [Display(Name = "Kategori")]
    public string Ad { get; set; } = "";

    // Ders kodlarının ön eki (PY-03 için "PY").
    [Required(ErrorMessage = "Kısaltma zorunludur.")]
    [StringLength(10, ErrorMessage = "En fazla 10 karakter.")]
    [Display(Name = "Kısaltma")]
    public string Kisaltma { get; set; } = "";

    [StringLength(500, ErrorMessage = "En fazla 500 karakter.")]
    [Display(Name = "Açıklama")]
    public string Aciklama { get; set; } = "";

    [Display(Name = "Sıra")]
    public int Sira { get; set; }

    public List<Egitim> Egitimler { get; set; } = new();
}
