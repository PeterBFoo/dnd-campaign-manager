using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndCampaign.Modules.Combat.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class EnemyGroupsAndEncounterDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_encounter_participants_shape",
                schema: "combat",
                table: "encounter_participants");

            migrationBuilder.CreateTable(
                name: "enemy_group_members",
                schema: "combat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    CurrentHitPoints = table.Column<int>(type: "integer", nullable: false),
                    MaximumHitPoints = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enemy_group_members", x => x.Id);
                    table.CheckConstraint("CK_enemy_group_members_hit_points", "\"MaximumHitPoints\" BETWEEN 1 AND 100000 AND \"CurrentHitPoints\" BETWEEN 0 AND \"MaximumHitPoints\"");
                    table.CheckConstraint("CK_enemy_group_members_ordinal", "\"Ordinal\" BETWEEN 1 AND 100");
                    table.ForeignKey(
                        name: "FK_enemy_group_members_encounter_participants_ParticipantId",
                        column: x => x.ParticipantId,
                        principalSchema: "combat",
                        principalTable: "encounter_participants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql(
                "INSERT INTO combat.enemy_group_members (\"Id\", \"ParticipantId\", \"Ordinal\", \"CurrentHitPoints\", \"MaximumHitPoints\") "
                + "SELECT gen_random_uuid(), \"Id\", 1, \"CurrentHitPoints\", \"MaximumHitPoints\" "
                + "FROM combat.encounter_participants WHERE \"Kind\" = 1;");

            migrationBuilder.DropColumn(
                name: "CurrentHitPoints",
                schema: "combat",
                table: "encounter_participants");

            migrationBuilder.DropColumn(
                name: "MaximumHitPoints",
                schema: "combat",
                table: "encounter_participants");

            migrationBuilder.AddCheckConstraint(
                name: "CK_encounter_participants_shape",
                schema: "combat",
                table: "encounter_participants",
                sql: "(\"Kind\" = 0 AND \"SourceCharacterId\" IS NOT NULL) OR (\"Kind\" = 1 AND \"SourceCharacterId\" IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_enemy_group_members_ParticipantId_Ordinal",
                schema: "combat",
                table: "enemy_group_members",
                columns: new[] { "ParticipantId", "Ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_encounter_participants_shape",
                schema: "combat",
                table: "encounter_participants");

            migrationBuilder.AddColumn<int>(
                name: "CurrentHitPoints",
                schema: "combat",
                table: "encounter_participants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaximumHitPoints",
                schema: "combat",
                table: "encounter_participants",
                type: "integer",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE combat.encounter_participants AS participant "
                + "SET \"CurrentHitPoints\" = member.\"CurrentHitPoints\", \"MaximumHitPoints\" = member.\"MaximumHitPoints\" "
                + "FROM combat.enemy_group_members AS member "
                + "WHERE participant.\"Id\" = member.\"ParticipantId\" AND member.\"Ordinal\" = 1;");

            migrationBuilder.DropTable(
                name: "enemy_group_members",
                schema: "combat");

            migrationBuilder.AddCheckConstraint(
                name: "CK_encounter_participants_shape",
                schema: "combat",
                table: "encounter_participants",
                sql: "(\"Kind\" = 0 AND \"SourceCharacterId\" IS NOT NULL AND \"CurrentHitPoints\" IS NULL AND \"MaximumHitPoints\" IS NULL) OR (\"Kind\" = 1 AND \"SourceCharacterId\" IS NULL AND \"CurrentHitPoints\" IS NOT NULL AND \"MaximumHitPoints\" IS NOT NULL AND \"CurrentHitPoints\" BETWEEN 0 AND \"MaximumHitPoints\" AND \"MaximumHitPoints\" BETWEEN 1 AND 100000)");
        }
    }
}
