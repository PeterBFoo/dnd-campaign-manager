using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndCampaign.Modules.Campaigns.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class InitialCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "campaigns");

            migrationBuilder.CreateTable(
                name: "campaigns",
                schema: "campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DmUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdventureModuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaigns", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_AdventureModuleId",
                schema: "campaigns",
                table: "campaigns",
                column: "AdventureModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_campaigns_DmUserId",
                schema: "campaigns",
                table: "campaigns",
                column: "DmUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campaigns",
                schema: "campaigns");
        }
    }
}
