using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentIngestionPipeline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChunkCount",
                table: "KnowledgeDocuments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ContentHash",
                table: "KnowledgeDocuments",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "KnowledgeDocuments",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "KnowledgeDocuments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "IndexedAt",
                table: "KnowledgeDocuments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "IngestionAttempts",
                table: "KnowledgeDocuments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "IngestionError",
                table: "KnowledgeDocuments",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IngestionStatus",
                table: "KnowledgeDocuments",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "KnowledgeDocuments",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "SourceData",
                table: "KnowledgeDocuments",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceFileName",
                table: "KnowledgeDocuments",
                type: "TEXT",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "KnowledgeDocuments",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeDocuments_IngestionStatus_NextAttemptAt",
                table: "KnowledgeDocuments",
                columns: new[] { "IngestionStatus", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_KnowledgeDocuments_IngestionStatus_NextAttemptAt",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "ChunkCount",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "ContentHash",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "IndexedAt",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "IngestionAttempts",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "IngestionError",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "IngestionStatus",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "SourceData",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "SourceFileName",
                table: "KnowledgeDocuments");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "KnowledgeDocuments");
        }
    }
}
