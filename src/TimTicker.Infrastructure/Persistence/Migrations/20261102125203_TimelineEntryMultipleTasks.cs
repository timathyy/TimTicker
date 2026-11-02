using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimTicker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TimelineEntryMultipleTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TimelineEntryTaskLinks",
                columns: table => new
                {
                    TimelineEntryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AddedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    RemovedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimelineEntryTaskLinks", x => new { x.TimelineEntryId, x.TaskItemId });
                    table.ForeignKey(
                        name: "FK_TimelineEntryTaskLinks_Tasks_TaskItemId",
                        column: x => x.TaskItemId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TimelineEntryTaskLinks_TimelineEntries_TimelineEntryId",
                        column: x => x.TimelineEntryId,
                        principalTable: "TimelineEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimelineEntryTaskLinks_TaskItemId_RemovedAtUtc",
                table: "TimelineEntryTaskLinks",
                columns: new[] { "TaskItemId", "RemovedAtUtc" });

            migrationBuilder.Sql(
                """
                INSERT INTO TimelineEntryTaskLinks (TimelineEntryId, TaskItemId, AddedAtUtc, RemovedAtUtc)
                SELECT Id, TaskItemId, CreatedAtUtc, NULL
                FROM TimelineEntries
                WHERE TaskItemId IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_TimelineEntries_Tasks_TaskItemId",
                table: "TimelineEntries");

            migrationBuilder.DropIndex(
                name: "IX_TimelineEntries_TaskItemId",
                table: "TimelineEntries");

            migrationBuilder.DropColumn(
                name: "TaskItemId",
                table: "TimelineEntries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TaskItemId",
                table: "TimelineEntries",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE TimelineEntries
                SET TaskItemId = (
                    SELECT TaskItemId
                    FROM TimelineEntryTaskLinks
                    WHERE TimelineEntryId = TimelineEntries.Id AND RemovedAtUtc IS NULL
                    ORDER BY AddedAtUtc
                    LIMIT 1
                );
                """);

            migrationBuilder.DropTable(
                name: "TimelineEntryTaskLinks");

            migrationBuilder.CreateIndex(
                name: "IX_TimelineEntries_TaskItemId",
                table: "TimelineEntries",
                column: "TaskItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_TimelineEntries_Tasks_TaskItemId",
                table: "TimelineEntries",
                column: "TaskItemId",
                principalTable: "Tasks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
