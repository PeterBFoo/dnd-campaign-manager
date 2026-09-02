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
        migrationBuilder.Sql("""
            ALTER TABLE adventure_catalog.adventure_modules
                ADD COLUMN IF NOT EXISTS "ChaptersVersion" bigint NOT NULL DEFAULT 1;

            CREATE TABLE IF NOT EXISTS adventure_catalog.adventure_chapters (
                "Id" uuid NOT NULL,
                "ModuleId" uuid NOT NULL,
                "Name" character varying(120) NOT NULL,
                "Description" character varying(20000),
                "Position" integer NOT NULL,
                "ChapterOriginKind" integer NOT NULL DEFAULT 0,
                "ChapterSourceReference" character varying(2000),
                "ChapterRightsBasis" character varying(2000) NOT NULL DEFAULT 'Migrated provisional chapter',
                "ChapterAttribution" character varying(2000),
                "ChapterVerifiedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "ChapterVerifiedByUserId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "LastModifiedByUserId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                "Version" bigint NOT NULL DEFAULT 1,
                CONSTRAINT "PK_adventure_chapters" PRIMARY KEY ("Id")
            );

            ALTER TABLE adventure_catalog.adventure_chapters
                ADD COLUMN IF NOT EXISTS "Description" character varying(20000),
                ADD COLUMN IF NOT EXISTS "ChapterOriginKind" integer NOT NULL DEFAULT 0,
                ADD COLUMN IF NOT EXISTS "ChapterSourceReference" character varying(2000),
                ADD COLUMN IF NOT EXISTS "ChapterRightsBasis" character varying(2000) NOT NULL DEFAULT 'Migrated provisional chapter',
                ADD COLUMN IF NOT EXISTS "ChapterAttribution" character varying(2000),
                ADD COLUMN IF NOT EXISTS "ChapterVerifiedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ADD COLUMN IF NOT EXISTS "ChapterVerifiedByUserId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ADD COLUMN IF NOT EXISTS "LastModifiedByUserId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                ADD COLUMN IF NOT EXISTS "Version" bigint NOT NULL DEFAULT 1;

            DO $body$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_adventure_chapters_adventure_modules_ModuleId') THEN
                    ALTER TABLE adventure_catalog.adventure_chapters
                        ADD CONSTRAINT "FK_adventure_chapters_adventure_modules_ModuleId"
                        FOREIGN KEY ("ModuleId") REFERENCES adventure_catalog.adventure_modules ("Id") ON DELETE CASCADE;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_adventure_chapters_position') THEN
                    ALTER TABLE adventure_catalog.adventure_chapters
                        ADD CONSTRAINT "CK_adventure_chapters_position" CHECK ("Position" >= 1);
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_adventure_chapters_version') THEN
                    ALTER TABLE adventure_catalog.adventure_chapters
                        ADD CONSTRAINT "CK_adventure_chapters_version" CHECK ("Version" >= 1);
                END IF;
            END
            $body$;

            CREATE UNIQUE INDEX IF NOT EXISTS "IX_adventure_chapters_ModuleId_Position"
                ON adventure_catalog.adventure_chapters ("ModuleId", "Position");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "adventure_chapters", schema: "adventure_catalog");
        migrationBuilder.DropColumn(name: "ChaptersVersion", schema: "adventure_catalog", table: "adventure_modules");
    }
}
