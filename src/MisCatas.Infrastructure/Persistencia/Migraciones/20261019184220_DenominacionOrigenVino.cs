using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MisCatas.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class DenominacionOrigenVino : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DenominacionOrigen",
                table: "Vinos",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DenominacionOrigen",
                table: "Vinos");
        }
    }
}
