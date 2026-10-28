using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MisCatas.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class TopeNota0a10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Votos_Nota_0_10",
                table: "Votos",
                sql: "[Nota] IS NULL OR ([Nota] >= 0 AND [Nota] <= 10)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Votos_Nota_0_10",
                table: "Votos");
        }
    }
}
