using EgitimPlatformu.Web.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace EgitimPlatformu.Web.Data;

public class UygulamaDbContext : IdentityDbContext<IdentityUser>
{
    public UygulamaDbContext(DbContextOptions<UygulamaDbContext> options) : base(options)
    {
    }

    public DbSet<Kategori> Kategoriler { get; set; } = default!;
    public DbSet<Egitim> Egitimler { get; set; } = default!;
    public DbSet<Gorev> Gorevler { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Kategori 1 — N Egitim
        modelBuilder.Entity<Egitim>()
            .HasOne(e => e.Kategori)
            .WithMany(k => k.Egitimler)
            .HasForeignKey(e => e.KategoriId)
            .OnDelete(DeleteBehavior.Restrict);

        // Egitim 1 — N Gorev; ders silinince görevleri de silinir.
        modelBuilder.Entity<Gorev>()
            .HasOne(g => g.Egitim)
            .WithMany(e => e.Gorevler)
            .HasForeignKey(g => g.EgitimId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Kategori>().HasIndex(k => k.Kisaltma).IsUnique();
        modelBuilder.Entity<Egitim>().HasIndex(e => e.Kod).IsUnique();
        // Bir derste aynı görev numarası iki kez olamaz.
        modelBuilder.Entity<Gorev>().HasIndex(g => new { g.EgitimId, g.No }).IsUnique();

        OrnekVeri.Ekle(modelBuilder);
    }
}
