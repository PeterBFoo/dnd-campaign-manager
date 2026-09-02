using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AdventureCatalogDbContext))]
[Migration("20260830230000_AdventureChapters")]
internal partial class AdventureChapters : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(name: "ChaptersVersion", schema: "adventure_catalog",
            table: "adventure_modules", type: "bigint", nullable: false, defaultValue: 1L);
        migrationBuilder.CreateTable(name: "adventure_chapters", schema: "adventure_catalog",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                Position = table.Column<int>(type: "integer", nullable: false),
                ChapterOriginKind = table.Column<int>(type: "integer", nullable: false),
                ChapterSourceReference = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                ChapterRightsBasis = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                ChapterAttribution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                ChapterVerifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ChapterVerifiedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastModifiedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
            }, constraints: table =>
            {
                table.PrimaryKey("PK_adventure_chapters", x => x.Id);
                table.ForeignKey("FK_adventure_chapters_adventure_modules_ModuleId", x => x.ModuleId,
                    principalSchema: "adventure_catalog", principalTable: "adventure_modules", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
                table.CheckConstraint("CK_adventure_chapters_position", "\"Position\" >= 1");
                table.CheckConstraint("CK_adventure_chapters_version", "\"Version\" >= 1");
            });
        migrationBuilder.CreateIndex(name: "IX_adventure_chapters_ModuleId_Position", schema: "adventure_catalog",
            table: "adventure_chapters", columns: new[] { "ModuleId", "Position" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "adventure_chapters", schema: "adventure_catalog");
        migrationBuilder.DropColumn(name: "ChaptersVersion", schema: "adventure_catalog", table: "adventure_modules");
    }
}
