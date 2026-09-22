using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeaLearning.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLearningCategoryAndModuleRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoriaId",
                table: "LearningModules",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LearningCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Color = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "#2F6E5C"),
                    Orden = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LearningCategories", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LearningModules_CategoriaId",
                table: "LearningModules",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_LearningCategories_Nombre",
                table: "LearningCategories",
                column: "Nombre");

            migrationBuilder.AddForeignKey(
                name: "FK_LearningModules_LearningCategories_CategoriaId",
                table: "LearningModules",
                column: "CategoriaId",
                principalTable: "LearningCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LearningModules_LearningCategories_CategoriaId",
                table: "LearningModules");

            migrationBuilder.DropTable(
                name: "LearningCategories");

            migrationBuilder.DropIndex(
                name: "IX_LearningModules_CategoriaId",
                table: "LearningModules");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "LearningModules");
        }
    }
}
