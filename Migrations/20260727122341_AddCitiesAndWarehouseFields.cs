using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WMS.Terminal.Migrations
{
    /// <inheritdoc />
    public partial class AddCitiesAndWarehouseFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Warehouses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CityId",
                table: "Warehouses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TravelDays",
                table: "Warehouses",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "CityId",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "TravelDays",
                table: "Warehouses");
        }
    }
}
