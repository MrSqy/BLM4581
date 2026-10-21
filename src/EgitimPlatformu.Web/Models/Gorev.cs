using System.ComponentModel.DataAnnotations;

namespace EgitimPlatformu.Web.Models;

// Dersteki bir alıştırma. No, test dosyasındaki test_gorev_N fonksiyonlarıyla eşleşir.
public class Gorev
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ders seçiniz.")]
    [Display(Name = "Ders")]
    public int? EgitimId { get; set; }

    public Egitim? Egitim { get; set; }

    [Required(ErrorMessage = "Görev numarası zorunludur.")]
    [Range(1, 100, ErrorMessage = "Görev numarası 1-100 olmalı.")]
    [Display(Name = "Görev No")]
    public int? No { get; set; }

    [Required(ErrorMessage = "Başlık zorunludur.")]
    [StringLength(200, ErrorMessage = "En fazla 200 karakter.")]
    [Display(Name = "Başlık")]
    public string Baslik { get; set; } = "";

    [Display(Name = "Açıklama")]
    public string Aciklama { get; set; } = "";
}
