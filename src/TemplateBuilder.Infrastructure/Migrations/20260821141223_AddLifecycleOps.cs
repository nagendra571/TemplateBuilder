using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TemplateBuilder.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLifecycleOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ExternalKey",
                table: "Templates",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "SourceView",
                table: "Templates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceViewSnapshot",
                table: "Templates",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("UPDATE dbo.Templates SET ExternalKey = NEWID() WHERE ExternalKey = '00000000-0000-0000-0000-000000000000'");

            migrationBuilder.CreateIndex(
                name: "IX_Templates_ExternalKey",
                table: "Templates",
                column: "ExternalKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Templates_ExternalKey",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "ExternalKey",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "SourceView",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "SourceViewSnapshot",
                table: "Templates");
        }
    }
}
