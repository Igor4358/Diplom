using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Terminal.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageIdToStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PackageId",
                table: "Stocks",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_PackageId",
                table: "Stocks",
                column: "PackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Stocks_Packages_PackageId",
                table: "Stocks",
                column: "PackageId",
                principalTable: "Packages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Stocks_Packages_PackageId",
                table: "Stocks");

            migrationBuilder.DropIndex(
                name: "IX_Stocks_PackageId",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "PackageId",
                table: "Stocks");
        }
    }
}
