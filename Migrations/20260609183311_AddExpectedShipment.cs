using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Terminal.Migrations
{
    /// <inheritdoc />
    public partial class AddExpectedShipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpectedShipments_Products_ProductId",
                table: "ExpectedShipments");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ExpectedShipments",
                table: "ExpectedShipments");

            migrationBuilder.RenameTable(
                name: "ExpectedShipments",
                newName: "ExpectedShipment");

            migrationBuilder.RenameIndex(
                name: "IX_ExpectedShipments_ProductId",
                table: "ExpectedShipment",
                newName: "IX_ExpectedShipment_ProductId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ExpectedShipment",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "ExpectedShipment",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExpectedShipment",
                table: "ExpectedShipment",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpectedShipment_Products_ProductId",
                table: "ExpectedShipment",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpectedShipment_Products_ProductId",
                table: "ExpectedShipment");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ExpectedShipment",
                table: "ExpectedShipment");

            migrationBuilder.RenameTable(
                name: "ExpectedShipment",
                newName: "ExpectedShipments");

            migrationBuilder.RenameIndex(
                name: "IX_ExpectedShipment_ProductId",
                table: "ExpectedShipments",
                newName: "IX_ExpectedShipments_ProductId");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ExpectedShipments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "InvoiceNumber",
                table: "ExpectedShipments",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddPrimaryKey(
                name: "PK_ExpectedShipments",
                table: "ExpectedShipments",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpectedShipments_Products_ProductId",
                table: "ExpectedShipments",
                column: "ProductId",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
