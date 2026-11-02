using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimTicker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DailyPlanning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    ClosedAtUtc = table.Column<long>(type: "INTEGER", nullable: true),
                    ReviewText = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    FinalCompletionRate = table.Column<decimal>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DailyPlanItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DailyPlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TaskItemId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    RemovedAtUtc = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyPlanItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DailyPlanItems_DailyPlans_DailyPlanId",
                        column: x => x.DailyPlanId,
                        principalTable: "DailyPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DailyPlanItems_Tasks_TaskItemId",
                        column: x => x.TaskItemId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyPlanItems_DailyPlanId_RemovedAtUtc_SortOrder",
                table: "DailyPlanItems",
                columns: new[] { "DailyPlanId", "RemovedAtUtc", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_DailyPlanItems_DailyPlanId_TaskItemId",
                table: "DailyPlanItems",
                columns: new[] { "DailyPlanId", "TaskItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyPlanItems_TaskItemId",
                table: "DailyPlanItems",
                column: "TaskItemId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyPlans_Date",
                table: "DailyPlans",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyPlanItems");

            migrationBuilder.DropTable(
                name: "DailyPlans");
        }
    }
}
