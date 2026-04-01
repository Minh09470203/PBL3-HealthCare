using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PBL3_HealthCare.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIsVideoAvailableToDoctor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVideoAvailable",
                table: "Doctors",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsVideoAvailable",
                table: "Doctors");
        }
    }
}
