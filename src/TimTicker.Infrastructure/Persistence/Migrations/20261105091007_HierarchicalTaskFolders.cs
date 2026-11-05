using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimTicker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HierarchicalTaskFolders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskTemplates_Name",
                table: "TaskTemplates");

            migrationBuilder.DropIndex(
                name: "IX_TaskFolders_Name",
                table: "TaskFolders");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "TaskFolders",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_Name",
                table: "TaskTemplates",
                column: "Name");

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

            migrationBuilder.AddForeignKey(
                name: "FK_TaskFolders_TaskFolders_ParentId",
                table: "TaskFolders",
                column: "ParentId",
                principalTable: "TaskFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskFolders_TaskFolders_ParentId",
                table: "TaskFolders");

            migrationBuilder.DropIndex(
                name: "IX_TaskTemplates_Name",
                table: "TaskTemplates");

            migrationBuilder.DropIndex(
                name: "IX_TaskFolders_Name",
                table: "TaskFolders");

            migrationBuilder.DropIndex(
                name: "IX_TaskFolders_ParentId_Name",
                table: "TaskFolders");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "TaskFolders");

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_Name",
                table: "TaskTemplates",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskFolders_Name",
                table: "TaskFolders",
                column: "Name",
                unique: true);
        }
    }
}
