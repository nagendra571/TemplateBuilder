using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TemplateBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSampleDataToTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SampleData",
                table: "Templates",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SampleData",
                table: "Templates");
        }
    }
}
