using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EgitimPlatformu.Web.Migrations
{
    /// <inheritdoc />
    public partial class DersIlerlemesi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ilerlemeler",
                columns: table => new
                {
                    KullaniciId = table.Column<string>(type: "text", nullable: false),
                    EgitimId = table.Column<int>(type: "integer", nullable: false),
                    TamamlanmaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ilerlemeler", x => new { x.KullaniciId, x.EgitimId });
                    table.ForeignKey(
                        name: "FK_Ilerlemeler_AspNetUsers_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Ilerlemeler_Egitimler_EgitimId",
                        column: x => x.EgitimId,
                        principalTable: "Egitimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Gonderimler_KullaniciId_EgitimId_GorevId",
                table: "Gonderimler",
                columns: new[] { "KullaniciId", "EgitimId", "GorevId" });

            migrationBuilder.CreateIndex(
                name: "IX_Ilerlemeler_EgitimId",
                table: "Ilerlemeler",
                column: "EgitimId");

            // Eski gönderimleri değiştirmeden, mevcut derslerin kanıtlanmış tamamlanmalarını taşı.
            migrationBuilder.Sql("""
                WITH gorev_gecisleri AS (
                    SELECT s."KullaniciId", g."EgitimId", g."Id", MIN(s."TarihUtc") AS ilk_gecis
                    FROM "Gonderimler" s
                    JOIN "Gorevler" g ON g."Id" = s."GorevId" AND g."EgitimId" = s."EgitimId"
                    WHERE g."Aktif" AND g."Tur" = 'kod' AND g."KontrolJson" IS NOT NULL
                        AND s."Basarili" AND s."Durum" = 'basarili'
                    GROUP BY s."KullaniciId", g."EgitimId", g."Id"
                )
                INSERT INTO "Ilerlemeler" ("KullaniciId", "EgitimId", "TamamlanmaUtc")
                SELECT p."KullaniciId", p."EgitimId",
                    CASE WHEN COUNT(*) = (
                        SELECT COUNT(*) FROM "Gorevler" g WHERE g."EgitimId" = p."EgitimId"
                            AND g."Aktif" AND g."Tur" = 'kod' AND g."KontrolJson" IS NOT NULL
                    ) THEN MAX(p.ilk_gecis) END
                FROM gorev_gecisleri p
                GROUP BY p."KullaniciId", p."EgitimId";

                INSERT INTO "Ilerlemeler" ("KullaniciId", "EgitimId", "TamamlanmaUtc")
                SELECT s."KullaniciId", s."EgitimId", MIN(s."TarihUtc")
                FROM "Gonderimler" s
                JOIN "Egitimler" e ON e."Id" = s."EgitimId"
                WHERE s."GorevId" IS NULL AND s."GorevNo" IS NULL AND s."Basarili"
                    AND s."Durum" = 'tamamlandi' AND TRIM(e."TestKodu") <> ''
                    AND NOT EXISTS (
                        SELECT 1 FROM "Gorevler" g WHERE g."EgitimId" = e."Id"
                            AND g."Aktif" AND g."Tur" <> 'eski-pytest'
                    )
                GROUP BY s."KullaniciId", s."EgitimId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ilerlemeler");

            migrationBuilder.DropIndex(
                name: "IX_Gonderimler_KullaniciId_EgitimId_GorevId",
                table: "Gonderimler");
        }
    }
}
