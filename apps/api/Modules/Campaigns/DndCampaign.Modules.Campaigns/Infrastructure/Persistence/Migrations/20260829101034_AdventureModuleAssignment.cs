using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndCampaign.Modules.Campaigns.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class AdventureModuleAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                schema: "campaigns",
                table: "campaigns",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddCheckConstraint(
                name: "CK_campaigns_version",
                schema: "campaigns",
                table: "campaigns",
                sql: "\"Version\" >= 1");

            migrationBuilder.AddForeignKey(
                name: "FK_campaigns_adventure_modules_AdventureModuleId",
                schema: "campaigns",
                table: "campaigns",
                column: "AdventureModuleId",
                principalSchema: "adventure_catalog",
                principalTable: "adventure_modules",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_campaigns_adventure_modules_AdventureModuleId",
                schema: "campaigns",
                table: "campaigns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_campaigns_version",
                schema: "campaigns",
                table: "campaigns");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "campaigns",
                table: "campaigns");
        }
    }
}
