using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AdventureCatalogDbContext))]
[Migration("20260829120000_InitialAdventureCatalog")]
internal partial class InitialAdventureCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "adventure_catalog");
        migrationBuilder.CreateTable(
            name: "adventure_modules",
            schema: "adventure_catalog",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                NormalizedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                TextOriginKind = table.Column<int>(type: "integer", nullable: false),
                TextSourceReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                TextRightsBasis = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                TextAttribution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                TextVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                TextVerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CoverObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                CoverContentType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                CoverSizeBytes = table.Column<long>(type: "bigint", nullable: true),
                CoverOriginKind = table.Column<int>(type: "integer", nullable: true),
                CoverSourceReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CoverRightsBasis = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CoverAttribution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                CoverVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CoverVerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_adventure_modules", x => x.Id);
                table.CheckConstraint("CK_adventure_modules_version", "\"Version\" >= 1");
                table.CheckConstraint("CK_adventure_modules_cover_shape", "(\"CoverObjectKey\" IS NULL AND \"CoverContentType\" IS NULL AND \"CoverSizeBytes\" IS NULL) OR (\"CoverObjectKey\" IS NOT NULL AND \"CoverContentType\" IS NOT NULL AND \"CoverSizeBytes\" BETWEEN 1 AND 10485760)");
            });

        migrationBuilder.CreateIndex(name: "IX_adventure_modules_NormalizedName", schema: "adventure_catalog", table: "adventure_modules", column: "NormalizedName", unique: true);
        migrationBuilder.CreateIndex(name: "IX_adventure_modules_UpdatedAt_Id", schema: "adventure_catalog", table: "adventure_modules", columns: new[] { "UpdatedAt", "Id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "adventure_modules", schema: "adventure_catalog");
}
