using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimTicker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HierarchicalTaskFoldersAndTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "Tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "Tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "TemplateOccurrenceDate",
                table: "Tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TemplateSequenceNumber",
                table: "Tasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TaskFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false, collation: "NOCASE"),
                    ParentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskFolders_TaskFolders_ParentId",
                        column: x => x.ParentId,
                        principalTable: "TaskFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TaskTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FolderId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false, collation: "NOCASE"),
                    TitlePattern = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    Schedule = table.Column<int>(type: "INTEGER", nullable: false),
                    RepeatInterval = table.Column<int>(type: "INTEGER", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    NextSequenceNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    LastGeneratedDate = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskTemplates_TaskFolders_FolderId",
                        column: x => x.FolderId,
                        principalTable: "TaskFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_FolderId_IsDeleted_CreatedAtUtc",
                table: "Tasks",
                columns: new[] { "FolderId", "IsDeleted", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TemplateId_TemplateOccurrenceDate",
                table: "Tasks",
                columns: new[] { "TemplateId", "TemplateOccurrenceDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_TemplateId_TemplateSequenceNumber",
                table: "Tasks",
                columns: new[] { "TemplateId", "TemplateSequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskFolders_IsDeleted_Kind_Name",
                table: "TaskFolders",
                columns: new[] { "IsDeleted", "Kind", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskFolders_Name",
                table: "TaskFolders",
                column: "Name",
                unique: true,
                filter: "ParentId IS NULL AND IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TaskFolders_ParentId_Name",
                table: "TaskFolders",
                columns: new[] { "ParentId", "Name" },
                unique: true,
                filter: "ParentId IS NOT NULL AND IsDeleted = 0");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_FolderId",
                table: "TaskTemplates",
                column: "FolderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_Name",
                table: "TaskTemplates",
                column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_TaskFolders_FolderId",
                table: "Tasks",
                column: "FolderId",
                principalTable: "TaskFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Tasks_TaskTemplates_TemplateId",
                table: "Tasks",
                column: "TemplateId",
                principalTable: "TaskTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_TaskFolders_FolderId",
                table: "Tasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Tasks_TaskTemplates_TemplateId",
                table: "Tasks");

            migrationBuilder.DropTable(
                name: "TaskTemplates");

            migrationBuilder.DropTable(
                name: "TaskFolders");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_FolderId_IsDeleted_CreatedAtUtc",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_TemplateId_TemplateOccurrenceDate",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_TemplateId_TemplateSequenceNumber",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "TemplateOccurrenceDate",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "TemplateSequenceNumber",
                table: "Tasks");
        }
    }
}
