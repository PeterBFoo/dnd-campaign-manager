using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DndCampaign.Modules.Journal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class InitialJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "journal");

            migrationBuilder.CreateTable(
                name: "journal_entries",
                schema: "journal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorCharacterId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorCharacterName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PaginationSequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_CampaignId_CreatedAt_PaginationSequence",
                schema: "journal",
                table: "journal_entries",
                columns: new[] { "CampaignId", "CreatedAt", "PaginationSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_CampaignId_CreatedByUserId",
                schema: "journal",
                table: "journal_entries",
                columns: new[] { "CampaignId", "CreatedByUserId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "journal_entries",
                schema: "journal");
        }
    }
}
