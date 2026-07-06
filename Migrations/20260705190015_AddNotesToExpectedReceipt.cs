using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Terminal.Migrations
{
    /// <inheritdoc />
    public partial class AddNotesToExpectedReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ExpectedReceipts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ExpectedReceipts");
        }
    }
}
