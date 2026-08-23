using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndCampaign.Modules.Characters.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class InitialCharacters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "characters");

            migrationBuilder.CreateTable(
                name: "characters",
                schema: "characters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ArmorClass = table.Column<int>(type: "integer", nullable: false),
                    Initiative = table.Column<int>(type: "integer", nullable: false),
                    ImageObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ImageContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ImageSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_characters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_characters_CampaignId",
                schema: "characters",
                table: "characters",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_characters_CampaignId_OwnerUserId",
                schema: "characters",
                table: "characters",
                columns: new[] { "CampaignId", "OwnerUserId" },
                unique: true,
                filter: "\"IsActive\" = TRUE AND \"OwnerUserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "characters",
                schema: "characters");
        }
    }
}
