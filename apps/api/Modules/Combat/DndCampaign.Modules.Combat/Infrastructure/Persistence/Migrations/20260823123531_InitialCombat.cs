using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndCampaign.Modules.Combat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class InitialCombat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "combat");

            migrationBuilder.CreateTable(
                name: "encounters",
                schema: "combat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Round = table.Column<int>(type: "integer", nullable: true),
                    CurrentParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    TiesResolved = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounters", x => x.Id);
                    table.CheckConstraint("CK_encounters_lifecycle", "(\"Status\" = 0 AND \"Round\" IS NULL AND \"CurrentParticipantId\" IS NULL AND \"ActivatedAt\" IS NULL AND \"FinishedAt\" IS NULL) OR (\"Status\" = 1 AND \"Round\" >= 1 AND \"CurrentParticipantId\" IS NOT NULL AND \"ActivatedAt\" IS NOT NULL AND \"FinishedAt\" IS NULL) OR (\"Status\" = 2 AND \"Round\" >= 1 AND \"CurrentParticipantId\" IS NOT NULL AND \"ActivatedAt\" IS NOT NULL AND \"FinishedAt\" IS NOT NULL)");
                    table.CheckConstraint("CK_encounters_status", "\"Status\" BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_encounters_version", "\"Version\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "encounter_participants",
                schema: "combat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    SourceCharacterId = table.Column<Guid>(type: "uuid", nullable: true),
                    NameSnapshot = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ArmorClass = table.Column<int>(type: "integer", nullable: false),
                    InitiativeTotal = table.Column<int>(type: "integer", nullable: false),
                    OrderPosition = table.Column<int>(type: "integer", nullable: false),
                    CurrentHitPoints = table.Column<int>(type: "integer", nullable: true),
                    MaximumHitPoints = table.Column<int>(type: "integer", nullable: true),
                    CreatedOrder = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_encounter_participants", x => x.Id);
                    table.CheckConstraint("CK_encounter_participants_armor", "\"ArmorClass\" BETWEEN 0 AND 40");
                    table.CheckConstraint("CK_encounter_participants_initiative", "\"InitiativeTotal\" BETWEEN -20 AND 30");
                    table.CheckConstraint("CK_encounter_participants_kind", "\"Kind\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_encounter_participants_order", "\"OrderPosition\" >= 0 AND \"CreatedOrder\" >= 1");
                    table.CheckConstraint("CK_encounter_participants_shape", "(\"Kind\" = 0 AND \"SourceCharacterId\" IS NOT NULL AND \"CurrentHitPoints\" IS NULL AND \"MaximumHitPoints\" IS NULL) OR (\"Kind\" = 1 AND \"SourceCharacterId\" IS NULL AND \"CurrentHitPoints\" IS NOT NULL AND \"MaximumHitPoints\" IS NOT NULL AND \"CurrentHitPoints\" BETWEEN 0 AND \"MaximumHitPoints\" AND \"MaximumHitPoints\" BETWEEN 1 AND 100000)");
                    table.ForeignKey(
                        name: "FK_encounter_participants_encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalSchema: "combat",
                        principalTable: "encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_encounter_participants_EncounterId_CreatedOrder",
                schema: "combat",
                table: "encounter_participants",
                columns: new[] { "EncounterId", "CreatedOrder" },
                unique: true);

            migrationBuilder.Sql(
                "ALTER TABLE combat.encounter_participants ADD CONSTRAINT \"AK_encounter_participants_order\" "
                + "UNIQUE (\"EncounterId\", \"OrderPosition\") DEFERRABLE INITIALLY DEFERRED;");

            migrationBuilder.CreateIndex(
                name: "IX_encounter_participants_EncounterId_SourceCharacterId",
                schema: "combat",
                table: "encounter_participants",
                columns: new[] { "EncounterId", "SourceCharacterId" },
                unique: true,
                filter: "\"SourceCharacterId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_encounters_CampaignId_Status",
                schema: "combat",
                table: "encounters",
                columns: new[] { "CampaignId", "Status" },
                unique: true,
                filter: "\"Status\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_encounters_CampaignId_Status_CreatedAt",
                schema: "combat",
                table: "encounters",
                columns: new[] { "CampaignId", "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "encounter_participants",
                schema: "combat");

            migrationBuilder.DropTable(
                name: "encounters",
                schema: "combat");
        }
    }
}
