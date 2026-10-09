using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeaLearning.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddObservationsCompositeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_PedagogicalNotes_StudentProfileId_VisibleAcudiente_FechaRegistro",
                table: "PedagogicalNotes",
                columns: new[] { "StudentProfileId", "VisibleAcudiente", "FechaRegistro" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PedagogicalNotes_StudentProfileId_VisibleAcudiente_FechaRegistro",
                table: "PedagogicalNotes");
        }
    }
}
