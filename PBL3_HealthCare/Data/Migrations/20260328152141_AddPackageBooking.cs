using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PBL3_HealthCare.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageBooking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageBookings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    HealthPackageId = table.Column<int>(type: "int", nullable: false),
                    BookingDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageBookings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageBookings_HealthPackages_HealthPackageId",
                        column: x => x.HealthPackageId,
                        principalTable: "HealthPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageBookings_Users_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageBookings_HealthPackageId",
                table: "PackageBookings",
                column: "HealthPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageBookings_PatientId",
                table: "PackageBookings",
                column: "PatientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PackageBookings");
        }
    }
}
