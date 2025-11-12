using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebShopV3.Migrations
{
    /// <inheritdoc />
    public partial class AddCompatibilityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FormFactor",
                table: "Components",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "MaxMemory",
                table: "Components",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MemorySlots",
                table: "Components",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemoryType",
                table: "Components",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PowerConnector",
                table: "Components",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Socket",
                table: "Components",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FormFactor",
                table: "Components");

            migrationBuilder.DropColumn(
                name: "MaxMemory",
                table: "Components");

            migrationBuilder.DropColumn(
                name: "MemorySlots",
                table: "Components");

            migrationBuilder.DropColumn(
                name: "MemoryType",
                table: "Components");

            migrationBuilder.DropColumn(
                name: "PowerConnector",
                table: "Components");

            migrationBuilder.DropColumn(
                name: "Socket",
                table: "Components");
        }
    }
}
