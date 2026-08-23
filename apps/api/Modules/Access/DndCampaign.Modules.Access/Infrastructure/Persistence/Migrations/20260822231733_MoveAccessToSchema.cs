using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndCampaign.Modules.Access.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    internal partial class MoveAccessToSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "access");

            migrationBuilder.RenameTable(
                name: "users",
                newName: "users",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "user_sessions",
                newName: "user_sessions",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "invitations",
                newName: "invitations",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "invitation_outbox",
                newName: "invitation_outbox",
                newSchema: "access");

            migrationBuilder.RenameTable(
                name: "campaign_memberships",
                newName: "campaign_memberships",
                newSchema: "access");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameTable(
                name: "users",
                schema: "access",
                newName: "users");

            migrationBuilder.RenameTable(
                name: "user_sessions",
                schema: "access",
                newName: "user_sessions");

            migrationBuilder.RenameTable(
                name: "invitations",
                schema: "access",
                newName: "invitations");

            migrationBuilder.RenameTable(
                name: "invitation_outbox",
                schema: "access",
                newName: "invitation_outbox");

            migrationBuilder.RenameTable(
                name: "campaign_memberships",
                schema: "access",
                newName: "campaign_memberships");
        }
    }
}
