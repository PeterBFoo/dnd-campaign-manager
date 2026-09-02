using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdventureLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_adventure_maps_ModuleId_Id",
                schema: "adventure_catalog",
                table: "adventure_maps",
                columns: new[] { "ModuleId", "Id" });

            migrationBuilder.AddUniqueConstraint(
                name: "AK_adventure_chapters_ModuleId_Id",
                schema: "adventure_catalog",
                table: "adventure_chapters",
                columns: new[] { "ModuleId", "Id" });

            migrationBuilder.CreateTable(
                name: "adventure_locations",
                schema: "adventure_catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: true),
                    DetailMapId = table.Column<Guid>(type: "uuid", nullable: true),
                    DetailMapModuleId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adventure_locations", x => x.Id);
                    table.UniqueConstraint("AK_adventure_locations_ModuleId_Id", x => new { x.ModuleId, x.Id });
                    table.CheckConstraint("CK_adventure_locations_detail_map_pair", "(\"DetailMapId\" IS NULL AND \"DetailMapModuleId\" IS NULL) OR (\"DetailMapId\" IS NOT NULL AND \"DetailMapModuleId\" IS NOT NULL)");
                    table.CheckConstraint("CK_adventure_locations_version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_adventure_locations_adventure_maps_DetailMapModuleId_Detail~",
                        columns: x => new { x.DetailMapModuleId, x.DetailMapId },
                        principalSchema: "adventure_catalog",
                        principalTable: "adventure_maps",
                        principalColumns: new[] { "ModuleId", "Id" },
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_adventure_locations_adventure_modules_ModuleId",
                        column: x => x.ModuleId,
                        principalSchema: "adventure_catalog",
                        principalTable: "adventure_modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adventure_location_chapters",
                schema: "adventure_catalog",
                columns: table => new
                {
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChapterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adventure_location_chapters", x => new { x.LocationId, x.ChapterId });
                    table.ForeignKey(
                        name: "FK_adventure_location_chapters_adventure_chapters_ModuleId_Cha~",
                        columns: x => new { x.ModuleId, x.ChapterId },
                        principalSchema: "adventure_catalog",
                        principalTable: "adventure_chapters",
                        principalColumns: new[] { "ModuleId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_adventure_location_chapters_adventure_locations_ModuleId_Lo~",
                        columns: x => new { x.ModuleId, x.LocationId },
                        principalSchema: "adventure_catalog",
                        principalTable: "adventure_locations",
                        principalColumns: new[] { "ModuleId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adventure_location_placements",
                schema: "adventure_catalog",
                columns: table => new
                {
                    MapId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    X = table.Column<decimal>(type: "numeric(18,15)", precision: 18, scale: 15, nullable: false),
                    Y = table.Column<decimal>(type: "numeric(18,15)", precision: 18, scale: 15, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adventure_location_placements", x => new { x.MapId, x.LocationId });
                    table.CheckConstraint("CK_adventure_location_placements_coordinates", "\"X\" BETWEEN 0 AND 1 AND \"Y\" BETWEEN 0 AND 1");
                    table.ForeignKey(
                        name: "FK_adventure_location_placements_adventure_locations_ModuleId_~",
                        columns: x => new { x.ModuleId, x.LocationId },
                        principalSchema: "adventure_catalog",
                        principalTable: "adventure_locations",
                        principalColumns: new[] { "ModuleId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_adventure_location_placements_adventure_maps_ModuleId_MapId",
                        columns: x => new { x.ModuleId, x.MapId },
                        principalSchema: "adventure_catalog",
                        principalTable: "adventure_maps",
                        principalColumns: new[] { "ModuleId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "adventure_points_of_interest",
                schema: "adventure_catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    X = table.Column<decimal>(type: "numeric(18,15)", precision: 18, scale: 15, nullable: true),
                    Y = table.Column<decimal>(type: "numeric(18,15)", precision: 18, scale: 15, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_adventure_points_of_interest", x => x.Id);
                    table.CheckConstraint("CK_adventure_points_of_interest_coordinates", "(\"X\" IS NULL AND \"Y\" IS NULL) OR (\"X\" IS NOT NULL AND \"Y\" IS NOT NULL AND \"X\" BETWEEN 0 AND 1 AND \"Y\" BETWEEN 0 AND 1)");
                    table.CheckConstraint("CK_adventure_points_of_interest_version", "\"Version\" >= 1");
                    table.ForeignKey(
                        name: "FK_adventure_points_of_interest_adventure_locations_ModuleId_L~",
                        columns: x => new { x.ModuleId, x.LocationId },
                        principalSchema: "adventure_catalog",
                        principalTable: "adventure_locations",
                        principalColumns: new[] { "ModuleId", "Id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_adventure_location_chapters_ModuleId_ChapterId",
                schema: "adventure_catalog",
                table: "adventure_location_chapters",
                columns: new[] { "ModuleId", "ChapterId" });

            migrationBuilder.CreateIndex(
                name: "IX_adventure_location_chapters_ModuleId_LocationId",
                schema: "adventure_catalog",
                table: "adventure_location_chapters",
                columns: new[] { "ModuleId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_adventure_location_placements_ModuleId_LocationId",
                schema: "adventure_catalog",
                table: "adventure_location_placements",
                columns: new[] { "ModuleId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_adventure_location_placements_ModuleId_MapId",
                schema: "adventure_catalog",
                table: "adventure_location_placements",
                columns: new[] { "ModuleId", "MapId" });

            migrationBuilder.CreateIndex(
                name: "IX_adventure_locations_DetailMapModuleId_DetailMapId",
                schema: "adventure_catalog",
                table: "adventure_locations",
                columns: new[] { "DetailMapModuleId", "DetailMapId" });

            migrationBuilder.CreateIndex(
                name: "IX_adventure_locations_ModuleId_UpdatedAt",
                schema: "adventure_catalog",
                table: "adventure_locations",
                columns: new[] { "ModuleId", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_adventure_points_of_interest_ModuleId_LocationId",
                schema: "adventure_catalog",
                table: "adventure_points_of_interest",
                columns: new[] { "ModuleId", "LocationId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "adventure_location_chapters",
                schema: "adventure_catalog");

            migrationBuilder.DropTable(
                name: "adventure_location_placements",
                schema: "adventure_catalog");

            migrationBuilder.DropTable(
                name: "adventure_points_of_interest",
                schema: "adventure_catalog");

            migrationBuilder.DropTable(
                name: "adventure_locations",
                schema: "adventure_catalog");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_adventure_maps_ModuleId_Id",
                schema: "adventure_catalog",
                table: "adventure_maps");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_adventure_chapters_ModuleId_Id",
                schema: "adventure_catalog",
                table: "adventure_chapters");
        }
    }
}
