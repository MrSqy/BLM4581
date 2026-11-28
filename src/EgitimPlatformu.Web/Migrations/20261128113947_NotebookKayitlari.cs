using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace EgitimPlatformu.Web.Migrations
{
    /// <inheritdoc />
    public partial class NotebookKayitlari : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notebooklar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    KullaniciId = table.Column<string>(type: "text", nullable: false),
                    EgitimId = table.Column<int>(type: "integer", nullable: true),
                    Baslik = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    IcerikJson = table.Column<string>(type: "text", nullable: false),
                    Surum = table.Column<int>(type: "integer", nullable: false),
                    GuncellendiUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notebooklar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notebooklar_AspNetUsers_KullaniciId",
                        column: x => x.KullaniciId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notebooklar_Egitimler_EgitimId",
                        column: x => x.EgitimId,
                        principalTable: "Egitimler",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notebooklar_EgitimId_GuncellendiUtc_Id",
                table: "Notebooklar",
                columns: new[] { "EgitimId", "GuncellendiUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Notebooklar_KullaniciId_GuncellendiUtc_Id",
                table: "Notebooklar",
                columns: new[] { "KullaniciId", "GuncellendiUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notebooklar");
        }
    }
}
