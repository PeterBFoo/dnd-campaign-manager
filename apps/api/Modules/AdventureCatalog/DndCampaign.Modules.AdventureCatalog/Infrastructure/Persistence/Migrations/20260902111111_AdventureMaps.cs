using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Persistence.Migrations;

public partial class AdventureMaps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS adventure_catalog.adventure_maps (
                "Id" uuid NOT NULL,
                "ModuleId" uuid NOT NULL,
                "Name" character varying(120) NOT NULL,
                "Description" character varying(10000),
                "ImageObjectKey" character varying(512),
                "ImageContentType" character varying(32),
                "ImageSizeBytes" bigint,
                "ImageWidth" integer,
                "ImageHeight" integer,
                "ImageOriginKind" integer,
                "ImageSourceReference" character varying(2000),
                "ImageRightsBasis" character varying(2000),
                "ImageAttribution" character varying(2000),
                "ImageVerifiedAt" timestamp with time zone,
                "ImageVerifiedByUserId" uuid,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                "LastModifiedByUserId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                "Version" bigint NOT NULL DEFAULT 1,
                CONSTRAINT "PK_adventure_maps" PRIMARY KEY ("Id")
            );

            ALTER TABLE adventure_catalog.adventure_maps
                ADD COLUMN IF NOT EXISTS "Description" character varying(10000),
                ADD COLUMN IF NOT EXISTS "ImageObjectKey" character varying(512),
                ADD COLUMN IF NOT EXISTS "ImageContentType" character varying(32),
                ADD COLUMN IF NOT EXISTS "ImageSizeBytes" bigint,
                ADD COLUMN IF NOT EXISTS "ImageWidth" integer,
                ADD COLUMN IF NOT EXISTS "ImageHeight" integer,
                ADD COLUMN IF NOT EXISTS "ImageOriginKind" integer,
                ADD COLUMN IF NOT EXISTS "ImageSourceReference" character varying(2000),
                ADD COLUMN IF NOT EXISTS "ImageRightsBasis" character varying(2000),
                ADD COLUMN IF NOT EXISTS "ImageAttribution" character varying(2000),
                ADD COLUMN IF NOT EXISTS "ImageVerifiedAt" timestamp with time zone,
                ADD COLUMN IF NOT EXISTS "ImageVerifiedByUserId" uuid,
                ADD COLUMN IF NOT EXISTS "CreatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
                ADD COLUMN IF NOT EXISTS "LastModifiedByUserId" uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
                ADD COLUMN IF NOT EXISTS "Version" bigint NOT NULL DEFAULT 1;

            CREATE TABLE IF NOT EXISTS adventure_catalog.adventure_map_chapters (
                "MapId" uuid NOT NULL,
                "ChapterId" uuid NOT NULL,
                CONSTRAINT "PK_adventure_map_chapters" PRIMARY KEY ("MapId", "ChapterId")
            );

            DO $body$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_adventure_maps_adventure_modules_ModuleId'
                      AND conrelid = 'adventure_catalog.adventure_maps'::regclass
                ) THEN
                    ALTER TABLE adventure_catalog.adventure_maps
                        ADD CONSTRAINT "FK_adventure_maps_adventure_modules_ModuleId"
                        FOREIGN KEY ("ModuleId") REFERENCES adventure_catalog.adventure_modules ("Id") ON DELETE CASCADE;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'CK_adventure_maps_version'
                      AND conrelid = 'adventure_catalog.adventure_maps'::regclass
                ) THEN
                    ALTER TABLE adventure_catalog.adventure_maps
                        ADD CONSTRAINT "CK_adventure_maps_version" CHECK ("Version" >= 1);
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_adventure_map_chapters_adventure_chapters_ChapterId'
                      AND conrelid = 'adventure_catalog.adventure_map_chapters'::regclass
                ) THEN
                    ALTER TABLE adventure_catalog.adventure_map_chapters
                        ADD CONSTRAINT "FK_adventure_map_chapters_adventure_chapters_ChapterId"
                        FOREIGN KEY ("ChapterId") REFERENCES adventure_catalog.adventure_chapters ("Id") ON DELETE CASCADE;
                END IF;
                IF NOT EXISTS (
                    SELECT 1 FROM pg_constraint
                    WHERE conname = 'FK_adventure_map_chapters_adventure_maps_MapId'
                      AND conrelid = 'adventure_catalog.adventure_map_chapters'::regclass
                ) THEN
                    ALTER TABLE adventure_catalog.adventure_map_chapters
                        ADD CONSTRAINT "FK_adventure_map_chapters_adventure_maps_MapId"
                        FOREIGN KEY ("MapId") REFERENCES adventure_catalog.adventure_maps ("Id") ON DELETE CASCADE;
                END IF;
            END
            $body$;

            CREATE INDEX IF NOT EXISTS "IX_adventure_map_chapters_ChapterId"
                ON adventure_catalog.adventure_map_chapters ("ChapterId");
            CREATE INDEX IF NOT EXISTS "IX_adventure_maps_ModuleId_UpdatedAt"
                ON adventure_catalog.adventure_maps ("ModuleId", "UpdatedAt");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "adventure_map_chapters", schema: "adventure_catalog");
        migrationBuilder.DropTable(name: "adventure_maps", schema: "adventure_catalog");
    }
}
