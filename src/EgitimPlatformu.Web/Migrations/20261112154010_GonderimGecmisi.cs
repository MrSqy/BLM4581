using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EgitimPlatformu.Web.Migrations
{
    /// <inheritdoc />
    public partial class GonderimGecmisi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Gonderimler",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KullaniciId = table.Column<string>(type: "text", nullable: false),
                    EgitimId = table.Column<int>(type: "integer", nullable: false),
                    Ders = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    GorevId = table.Column<int>(type: "integer", nullable: true),
                    GorevNo = table.Column<int>(type: "integer", nullable: true),
                    GorevBasligi = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Kod = table.Column<string>(type: "character varying(32000)", maxLength: 32000, nullable: false),
                    Cikti = table.Column<string>(type: "text", nullable: false),
                    Hata = table.Column<string>(type: "text", nullable: false),
                    Durum = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Basarili = table.Column<bool>(type: "boolean", nullable: false),
                    CikisKodu = table.Column<int>(type: "integer", nullable: true),
                    SureMs = table.Column<long>(type: "bigint", nullable: false),
                    TarihUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Gonderimler", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Gonderimler_AspNetUsers_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Gonderimler_Gorevler_GorevId",
                        column: x => x.GorevId,
                        principalTable: "Gorevler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Gonderimler_GorevId",
                table: "Gonderimler",
                column: "GorevId");

            migrationBuilder.CreateIndex(
                name: "IX_Gonderimler_KullaniciId_TarihUtc_Id",
                table: "Gonderimler",
                columns: new[] { "KullaniciId", "TarihUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Gonderimler");
        }
    }
}
