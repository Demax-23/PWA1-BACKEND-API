using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeaLearning.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSensoryTimerPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ModoTemporizador",
                table: "SensoryPreferences",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "tiempo-extendido");

            migrationBuilder.AddColumn<int>(
                name: "SegundosAvisoPrevio",
                table: "SensoryPreferences",
                type: "int",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.Sql(
                "UPDATE [SensoryPreferences] SET [ModoTemporizador] = N'sin-temporizador' WHERE [OcultarTemporizadores] = 1;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ModoTemporizador",
                table: "SensoryPreferences");

            migrationBuilder.DropColumn(
                name: "SegundosAvisoPrevio",
                table: "SensoryPreferences");
        }
    }
}
