using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PBL3_HealthCare.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePackageBooking_WalkIn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedOptions",
                table: "PackageBookings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedOptions",
                table: "PackageBookings");
        }
    }
}
