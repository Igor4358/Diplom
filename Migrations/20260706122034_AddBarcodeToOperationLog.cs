using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Terminal.Migrations
{
    /// <inheritdoc />
    public partial class AddBarcodeToOperationLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Barcode",
                table: "OperationLogs",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Barcode",
                table: "OperationLogs");
        }
    }
}
