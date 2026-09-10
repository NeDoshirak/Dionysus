using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dionysus.Api.Infrastructure.Persistence.Migrations;

public partial class AddTranscriptSegments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "VoiceRecordings"
            ADD COLUMN IF NOT EXISTS "Language" text;

            CREATE TABLE IF NOT EXISTS "TranscriptSegments"
            (
                "Id" uuid NOT NULL CONSTRAINT "PK_TranscriptSegments" PRIMARY KEY,
                "VoiceRecordingId" uuid NOT NULL,
                "StartSeconds" double precision NOT NULL,
                "EndSeconds" double precision NOT NULL,
                "Text" text NOT NULL,
                CONSTRAINT "FK_TranscriptSegments_VoiceRecordings_VoiceRecordingId"
                    FOREIGN KEY ("VoiceRecordingId")
                    REFERENCES "VoiceRecordings" ("Id")
                    ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS "IX_TranscriptSegments_VoiceRecordingId"
            ON "TranscriptSegments" ("VoiceRecordingId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "TranscriptSegments";
            ALTER TABLE "VoiceRecordings" DROP COLUMN IF EXISTS "Language";
            """);
    }
}

