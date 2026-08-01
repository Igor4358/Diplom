using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Terminal.Migrations
{
    /// <inheritdoc />
    public partial class AddWarehouseIdToStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_Cells_CellId",
                table: "Stocks");

            migrationBuilder.AlterColumn<int>(
                name: "CellId",
                table: "Stocks",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "WarehouseId",
                table: "Stocks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_WarehouseId",
                table: "Stocks",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_Cells_CellId",
                table: "Stocks",
                column: "CellId",
                principalTable: "Cells",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_Warehouses_WarehouseId",
                table: "Stocks",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_Cells_CellId",
                table: "Stocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_Warehouses_WarehouseId",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_WarehouseId",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "Stocks");

            migrationBuilder.AlterColumn<int>(
                name: "CellId",
                table: "Stocks",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_Cells_CellId",
                table: "Stocks",
                column: "CellId",
                principalTable: "Cells",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
