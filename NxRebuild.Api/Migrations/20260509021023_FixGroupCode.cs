using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NxRebuild.Api.Migrations
{
    /// <inheritdoc />
    public partial class FixGroupCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GroupCode",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GroupCode",
                table: "AspNetUsers");
        }
    }
}
