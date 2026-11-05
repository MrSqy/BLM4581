using Microsoft.EntityFrameworkCore.Migrations;
#nullable disable
namespace EgitimPlatformu.Web.Migrations
{
    public partial class GorevBazliKontrol : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // İçerik güncellenmez; eski görevler açık v2 aktarımına kadar korunur.
            migrationBuilder.AddColumn<bool>(name: "Aktif", table: "Gorevler", type: "boolean", nullable: false, defaultValue: true);
            migrationBuilder.AddColumn<string>(name: "Tur", table: "Gorevler", type: "text", nullable: false, defaultValue: "eski-pytest");
            migrationBuilder.AddColumn<string>(name: "BaslangicDosyasi", table: "Gorevler", type: "text", nullable: true);
            migrationBuilder.AddColumn<string>(name: "BaslangicKodu", table: "Gorevler", type: "text", nullable: false, defaultValue: "");
            migrationBuilder.AddColumn<string>(name: "KontrolJson", table: "Gorevler", type: "text", nullable: true);
        }
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var column in new[] { "Aktif", "Tur", "BaslangicDosyasi", "BaslangicKodu", "KontrolJson" })
                migrationBuilder.DropColumn(name: column, table: "Gorevler");
        }
    }
}
