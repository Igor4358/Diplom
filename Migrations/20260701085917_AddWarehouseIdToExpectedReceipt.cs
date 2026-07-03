using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Terminal.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseIdToExpectedReceipt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WarehouseId",
                table: "ExpectedReceipts",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExpectedReceipts_WarehouseId",
                table: "ExpectedReceipts",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpectedReceipts_Warehouses_WarehouseId",
                table: "ExpectedReceipts",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpectedReceipts_Warehouses_WarehouseId",
                table: "ExpectedReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ExpectedReceipts_WarehouseId",
                table: "ExpectedReceipts");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "ExpectedReceipts");
        }
    }
}
