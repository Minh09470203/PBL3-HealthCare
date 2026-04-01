using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PBL3_HealthCare.Data.Migrations
{
    /// <inheritdoc />
    public partial class UpdateMedicalRecordTask3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DoctorNotes",
                table: "MedicalRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Symptoms",
                table: "MedicalRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DoctorNotes",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "Symptoms",
                table: "MedicalRecords");
        }
    }
}
