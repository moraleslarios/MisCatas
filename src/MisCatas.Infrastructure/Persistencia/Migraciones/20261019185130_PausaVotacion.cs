using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MisCatas.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class PausaVotacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SegundosPausaRestantes",
                table: "CataVinos",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SegundosPausaRestantes",
                table: "CataVinos");
        }
    }
}
