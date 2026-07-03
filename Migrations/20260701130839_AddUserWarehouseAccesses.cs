using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Terminal.Migrations
{
    /// <inheritdoc />
    public partial class AddUserWarehouseAccesses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserWarehouseAcess_Users_UserId",
                table: "UserWarehouseAcess");

            migrationBuilder.DropForeignKey(
                name: "FK_UserWarehouseAcess_Warehouses_WarehouseId",
                table: "UserWarehouseAcess");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserWarehouseAcess",
                table: "UserWarehouseAcess");

            migrationBuilder.RenameTable(
                name: "UserWarehouseAcess",
                newName: "UserWarehouseAccesses");

            migrationBuilder.RenameIndex(
                name: "IX_UserWarehouseAcess_WarehouseId",
                table: "UserWarehouseAccesses",
                newName: "IX_UserWarehouseAccesses_WarehouseId");

            migrationBuilder.RenameIndex(
                name: "IX_UserWarehouseAcess_UserId",
                table: "UserWarehouseAccesses",
                newName: "IX_UserWarehouseAccesses_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserWarehouseAccesses",
                table: "UserWarehouseAccesses",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserWarehouseAccesses_Users_UserId",
                table: "UserWarehouseAccesses",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserWarehouseAccesses_Warehouses_WarehouseId",
                table: "UserWarehouseAccesses",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserWarehouseAccesses_Users_UserId",
                table: "UserWarehouseAccesses");

            migrationBuilder.DropForeignKey(
                name: "FK_UserWarehouseAccesses_Warehouses_WarehouseId",
                table: "UserWarehouseAccesses");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserWarehouseAccesses",
                table: "UserWarehouseAccesses");

            migrationBuilder.RenameTable(
                name: "UserWarehouseAccesses",
                newName: "UserWarehouseAcess");

            migrationBuilder.RenameIndex(
                name: "IX_UserWarehouseAccesses_WarehouseId",
                table: "UserWarehouseAcess",
                newName: "IX_UserWarehouseAcess_WarehouseId");

            migrationBuilder.RenameIndex(
                name: "IX_UserWarehouseAccesses_UserId",
                table: "UserWarehouseAcess",
                newName: "IX_UserWarehouseAcess_UserId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserWarehouseAcess",
                table: "UserWarehouseAcess",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_UserWarehouseAcess_Users_UserId",
                table: "UserWarehouseAcess",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_UserWarehouseAcess_Warehouses_WarehouseId",
                table: "UserWarehouseAcess",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
