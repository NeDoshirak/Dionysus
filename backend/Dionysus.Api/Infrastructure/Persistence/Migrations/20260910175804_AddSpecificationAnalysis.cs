using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Dionysus.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecificationAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VoiceRecordings_ProjectEntityId",
                table: "VoiceRecordings");

            migrationBuilder.AddColumn<string>(
                name: "CleanedText",
                table: "TranscriptSegments",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SpecificationAnalyses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: true),
                    Stage0RawResponse = table.Column<string>(type: "text", nullable: true),
                    Stage1RawResponse = table.Column<string>(type: "text", nullable: true),
                    Stage2RawResponse = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecificationAnalyses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecificationAnalyses_Projects_ProjectEntityId",
                        column: x => x.ProjectEntityId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisRelations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecificationAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRelations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisRelations_SpecificationAnalyses_SpecificationAnalys~",
                        column: x => x.SpecificationAnalysisId,
                        principalTable: "SpecificationAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisTopics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecificationAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisTopics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisTopics_SpecificationAnalyses_SpecificationAnalysisId",
                        column: x => x.SpecificationAnalysisId,
                        principalTable: "SpecificationAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisStatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecificationAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisTopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsBusinessContext = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisStatements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnalysisStatements_AnalysisTopics_AnalysisTopicId",
                        column: x => x.AnalysisTopicId,
                        principalTable: "AnalysisTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AnalysisStatements_SpecificationAnalyses_SpecificationAnaly~",
                        column: x => x.SpecificationAnalysisId,
                        principalTable: "SpecificationAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpecificationFunctions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecificationAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisTopicId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecificationFunctions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecificationFunctions_AnalysisTopics_AnalysisTopicId",
                        column: x => x.AnalysisTopicId,
                        principalTable: "AnalysisTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SpecificationFunctions_SpecificationAnalyses_SpecificationA~",
                        column: x => x.SpecificationAnalysisId,
                        principalTable: "SpecificationAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisRelationSourceStatements",
                columns: table => new
                {
                    AnalysisRelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisStatementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRelationSourceStatements", x => new { x.AnalysisRelationId, x.AnalysisStatementId });
                    table.ForeignKey(
                        name: "FK_AnalysisRelationSourceStatements_AnalysisRelations_Analysis~",
                        column: x => x.AnalysisRelationId,
                        principalTable: "AnalysisRelations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnalysisRelationSourceStatements_AnalysisStatements_Analysi~",
                        column: x => x.AnalysisStatementId,
                        principalTable: "AnalysisStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisRelationTargetStatements",
                columns: table => new
                {
                    AnalysisRelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisStatementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisRelationTargetStatements", x => new { x.AnalysisRelationId, x.AnalysisStatementId });
                    table.ForeignKey(
                        name: "FK_AnalysisRelationTargetStatements_AnalysisRelations_Analysis~",
                        column: x => x.AnalysisRelationId,
                        principalTable: "AnalysisRelations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnalysisRelationTargetStatements_AnalysisStatements_Analysi~",
                        column: x => x.AnalysisStatementId,
                        principalTable: "AnalysisStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnalysisStatementSegments",
                columns: table => new
                {
                    AnalysisStatementId = table.Column<Guid>(type: "uuid", nullable: false),
                    TranscriptSegmentId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnalysisStatementSegments", x => new { x.AnalysisStatementId, x.TranscriptSegmentId });
                    table.ForeignKey(
                        name: "FK_AnalysisStatementSegments_AnalysisStatements_AnalysisStatem~",
                        column: x => x.AnalysisStatementId,
                        principalTable: "AnalysisStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnalysisStatementSegments_TranscriptSegments_TranscriptSegm~",
                        column: x => x.TranscriptSegmentId,
                        principalTable: "TranscriptSegments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpecificationFunctionStatements",
                columns: table => new
                {
                    SpecificationFunctionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisStatementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecificationFunctionStatements", x => new { x.SpecificationFunctionId, x.AnalysisStatementId });
                    table.ForeignKey(
                        name: "FK_SpecificationFunctionStatements_AnalysisStatements_Analysis~",
                        column: x => x.AnalysisStatementId,
                        principalTable: "AnalysisStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpecificationFunctionStatements_SpecificationFunctions_Spec~",
                        column: x => x.SpecificationFunctionId,
                        principalTable: "SpecificationFunctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpecificationItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecificationAnalysisId = table.Column<Guid>(type: "uuid", nullable: false),
                    SpecificationFunctionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsManual = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecificationItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecificationItems_SpecificationAnalyses_SpecificationAnaly~",
                        column: x => x.SpecificationAnalysisId,
                        principalTable: "SpecificationAnalyses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpecificationItems_SpecificationFunctions_SpecificationFunc~",
                        column: x => x.SpecificationFunctionId,
                        principalTable: "SpecificationFunctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpecificationItemStatements",
                columns: table => new
                {
                    SpecificationItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    AnalysisStatementId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecificationItemStatements", x => new { x.SpecificationItemId, x.AnalysisStatementId });
                    table.ForeignKey(
                        name: "FK_SpecificationItemStatements_AnalysisStatements_AnalysisStat~",
                        column: x => x.AnalysisStatementId,
                        principalTable: "AnalysisStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SpecificationItemStatements_SpecificationItems_Specificatio~",
                        column: x => x.SpecificationItemId,
                        principalTable: "SpecificationItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VoiceRecordings_ProjectEntityId",
                table: "VoiceRecordings",
                column: "ProjectEntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRelations_SpecificationAnalysisId_ExternalId",
                table: "AnalysisRelations",
                columns: new[] { "SpecificationAnalysisId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRelationSourceStatements_AnalysisStatementId",
                table: "AnalysisRelationSourceStatements",
                column: "AnalysisStatementId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisRelationTargetStatements_AnalysisStatementId",
                table: "AnalysisRelationTargetStatements",
                column: "AnalysisStatementId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisStatements_AnalysisTopicId",
                table: "AnalysisStatements",
                column: "AnalysisTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisStatements_SpecificationAnalysisId_ExternalId",
                table: "AnalysisStatements",
                columns: new[] { "SpecificationAnalysisId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisStatementSegments_TranscriptSegmentId",
                table: "AnalysisStatementSegments",
                column: "TranscriptSegmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisTopics_SpecificationAnalysisId_ExternalId",
                table: "AnalysisTopics",
                columns: new[] { "SpecificationAnalysisId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecificationAnalyses_ProjectEntityId",
                table: "SpecificationAnalyses",
                column: "ProjectEntityId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecificationFunctions_AnalysisTopicId",
                table: "SpecificationFunctions",
                column: "AnalysisTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecificationFunctions_SpecificationAnalysisId",
                table: "SpecificationFunctions",
                column: "SpecificationAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecificationFunctionStatements_AnalysisStatementId",
                table: "SpecificationFunctionStatements",
                column: "AnalysisStatementId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecificationItems_SpecificationAnalysisId",
                table: "SpecificationItems",
                column: "SpecificationAnalysisId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecificationItems_SpecificationFunctionId",
                table: "SpecificationItems",
                column: "SpecificationFunctionId");

            migrationBuilder.CreateIndex(
                name: "IX_SpecificationItemStatements_AnalysisStatementId",
                table: "SpecificationItemStatements",
                column: "AnalysisStatementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnalysisRelationSourceStatements");

            migrationBuilder.DropTable(
                name: "AnalysisRelationTargetStatements");

            migrationBuilder.DropTable(
                name: "AnalysisStatementSegments");

            migrationBuilder.DropTable(
                name: "SpecificationFunctionStatements");

            migrationBuilder.DropTable(
                name: "SpecificationItemStatements");

            migrationBuilder.DropTable(
                name: "AnalysisRelations");

            migrationBuilder.DropTable(
                name: "AnalysisStatements");

            migrationBuilder.DropTable(
                name: "SpecificationItems");

            migrationBuilder.DropTable(
                name: "SpecificationFunctions");

            migrationBuilder.DropTable(
                name: "AnalysisTopics");

            migrationBuilder.DropTable(
                name: "SpecificationAnalyses");

            migrationBuilder.DropIndex(
                name: "IX_VoiceRecordings_ProjectEntityId",
                table: "VoiceRecordings");

            migrationBuilder.DropColumn(
                name: "CleanedText",
                table: "TranscriptSegments");

            migrationBuilder.CreateIndex(
                name: "IX_VoiceRecordings_ProjectEntityId",
                table: "VoiceRecordings",
                column: "ProjectEntityId");
        }
    }
}
