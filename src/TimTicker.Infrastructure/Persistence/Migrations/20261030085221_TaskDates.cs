using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimTicker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TaskDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Tasks SET Status = 0 WHERE Status = 2;");

            migrationBuilder.AddColumn<DateOnly>(
                name: "CompletedDate",
                table: "Tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly?>(
                name: "CreatedDate",
                table: "Tasks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE Tasks SET CreatedDate = date('now', 'localtime') WHERE CreatedDate IS NULL;");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "CreatedDate",
                table: "Tasks",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "TEXT",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedDate",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "CreatedDate",
                table: "Tasks");
        }
    }
}
