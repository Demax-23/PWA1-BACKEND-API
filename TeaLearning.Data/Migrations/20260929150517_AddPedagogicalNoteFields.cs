using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeaLearning.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPedagogicalNoteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "PedagogicalNotes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Socioemocional");

            migrationBuilder.AddColumn<Guid>(
                name: "AssignmentId",
                table: "PedagogicalNotes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecomendacionCasa",
                table: "PedagogicalNotes",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "PedagogicalNotes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Observacion");

            migrationBuilder.AddColumn<string>(
                name: "Titulo",
                table: "PedagogicalNotes",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "VisibleAcudiente",
                table: "PedagogicalNotes",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Area",
                table: "PedagogicalNotes");

            migrationBuilder.DropColumn(
                name: "AssignmentId",
                table: "PedagogicalNotes");

            migrationBuilder.DropColumn(
                name: "RecomendacionCasa",
                table: "PedagogicalNotes");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "PedagogicalNotes");

            migrationBuilder.DropColumn(
                name: "Titulo",
                table: "PedagogicalNotes");

            migrationBuilder.DropColumn(
                name: "VisibleAcudiente",
                table: "PedagogicalNotes");
        }
    }
}
