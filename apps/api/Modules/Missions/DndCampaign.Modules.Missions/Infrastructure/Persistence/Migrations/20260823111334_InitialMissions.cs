using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DndCampaign.Modules.Missions.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class InitialMissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "missions");

            migrationBuilder.CreateTable(
                name: "missions",
                schema: "missions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorType = table.Column<int>(type: "integer", nullable: false),
                    AuthorCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    AuthorCharacterName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsMain = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    SortSequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_missions", x => x.Id);
                    table.CheckConstraint("CK_missions_author", "(\"AuthorType\" = 0 AND \"AuthorCharacterId\" IS NULL AND \"AuthorCharacterName\" IS NULL) OR (\"AuthorType\" = 1 AND \"AuthorCharacterId\" IS NOT NULL AND \"AuthorCharacterName\" IS NOT NULL)");
                    table.CheckConstraint("CK_missions_main_active", "NOT \"IsMain\" OR \"Status\" = 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_missions_CampaignId",
                schema: "missions",
                table: "missions",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_missions_CampaignId_CreatedByUserId",
                schema: "missions",
                table: "missions",
                columns: new[] { "CampaignId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_missions_CampaignId_IsMain",
                schema: "missions",
                table: "missions",
                columns: new[] { "CampaignId", "IsMain" },
                unique: true,
                filter: "\"IsMain\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_missions_CampaignId_Status_CreatedAt_SortSequence",
                schema: "missions",
                table: "missions",
                columns: new[] { "CampaignId", "Status", "CreatedAt", "SortSequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "missions",
                schema: "missions");
        }
    }
}
