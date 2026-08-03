using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LynqMentrics.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivacyCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrivacyConsents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    IpHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    ConsentType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    Granted = table.Column<bool>(type: "INTEGER", nullable: false),
                    ConsentVersion = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivacyConsents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyConsents_GrantedAt",
                table: "PrivacyConsents",
                column: "GrantedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PrivacyConsents_UserId",
                table: "PrivacyConsents",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrivacyConsents");
        }
    }
}
