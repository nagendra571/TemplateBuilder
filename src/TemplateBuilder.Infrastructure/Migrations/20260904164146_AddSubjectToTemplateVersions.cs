using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TemplateBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubjectToTemplateVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "TemplateVersions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Subject",
                table: "TemplateVersions");
        }
    }
}
