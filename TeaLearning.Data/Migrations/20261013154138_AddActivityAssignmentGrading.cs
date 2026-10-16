using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeaLearning.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddActivityAssignmentGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Calificacion",
                table: "ActivityAssignments",
                type: "decimal(3,1)",
                precision: 3,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CalificadoPorDocenteId",
                table: "ActivityAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ComentarioCalificacion",
                table: "ActivityAssignments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FechaCalificacion",
                table: "ActivityAssignments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ActivityAssignments_CalificadoPorDocenteId",
                table: "ActivityAssignments",
                column: "CalificadoPorDocenteId");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityAssignments_TeacherProfiles_CalificadoPorDocenteId",
                table: "ActivityAssignments",
                column: "CalificadoPorDocenteId",
                principalTable: "TeacherProfiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityAssignments_TeacherProfiles_CalificadoPorDocenteId",
                table: "ActivityAssignments");

            migrationBuilder.DropIndex(
                name: "IX_ActivityAssignments_CalificadoPorDocenteId",
                table: "ActivityAssignments");

            migrationBuilder.DropColumn(
                name: "Calificacion",
                table: "ActivityAssignments");

            migrationBuilder.DropColumn(
                name: "CalificadoPorDocenteId",
                table: "ActivityAssignments");

            migrationBuilder.DropColumn(
                name: "ComentarioCalificacion",
                table: "ActivityAssignments");

            migrationBuilder.DropColumn(
                name: "FechaCalificacion",
                table: "ActivityAssignments");
        }
    }
}
