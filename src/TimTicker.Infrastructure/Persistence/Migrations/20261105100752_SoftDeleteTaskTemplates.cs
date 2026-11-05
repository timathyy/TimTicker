using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TimTicker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SoftDeleteTaskTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TaskTemplates",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TaskTemplates_IsDeleted_Name",
                table: "TaskTemplates",
                columns: new[] { "IsDeleted", "Name" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TaskTemplates_IsDeleted_Name",
                table: "TaskTemplates");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TaskTemplates");
        }
    }
}
