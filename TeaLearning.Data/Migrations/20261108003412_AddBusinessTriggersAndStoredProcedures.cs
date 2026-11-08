using Microsoft.EntityFrameworkCore.Migrations;
using TeaLearning.Data.SqlScripts;

#nullable disable

namespace TeaLearning.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessTriggersAndStoredProcedures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var sqlBatch in SqlProgrammabilityDefinitions.GetAllCreateOrAlterBatches())
            {
                migrationBuilder.Sql(sqlBatch);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var dropBatch in SqlProgrammabilityDefinitions.GetAllDropBatches())
            {
                migrationBuilder.Sql(dropBatch);
            }
        }
    }
}
