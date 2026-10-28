using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MisCatas.Infrastructure.Persistencia.Migraciones
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Configuracion",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    TiempoVotoSegundos = table.Column<int>(type: "int", nullable: false),
                    PorcentajeAlertaRoja = table.Column<int>(type: "int", nullable: false),
                    SegundosRevelacionFinal = table.Column<int>(type: "int", nullable: false),
                    PermitirAvanzarSinTodosLosVotos = table.Column<bool>(type: "bit", nullable: false),
                    PoliticaNoVoto = table.Column<int>(type: "int", nullable: false),
                    NotaPorDefectoNoVoto = table.Column<decimal>(type: "decimal(4,1)", nullable: true),
                    ComentarioAutomaticoNoVoto = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasoDecimalNota = table.Column<decimal>(type: "decimal(3,1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuracion", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Login = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Apellidos = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Telefono = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Rol = table.Column<int>(type: "int", nullable: false),
                    DebeCambiarPassword = table.Column<bool>(type: "bit", nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Vinos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Anio = table.Column<int>(type: "int", nullable: true),
                    Clase = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Dueno = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PrecioEstimado = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    Bodega = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Comentarios = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Foto = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    FotoContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Etiqueta = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    EtiquetaContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vinos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Catas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Lugar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Fecha = table.Column<DateTime>(type: "date", nullable: false),
                    Comentarios = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Estado = table.Column<int>(type: "int", nullable: false),
                    FechaApertura = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FechaFinalizacion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VinoActualId = table.Column<int>(type: "int", nullable: true),
                    AdminControlaId = table.Column<int>(type: "int", nullable: true),
                    TiempoVotoSegundos = table.Column<int>(type: "int", nullable: true),
                    RevelacionActiva = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Catas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Catas_Usuarios_AdminControlaId",
                        column: x => x.AdminControlaId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Catas_Vinos_VinoActualId",
                        column: x => x.VinoActualId,
                        principalTable: "Vinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CataUsuarios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CataId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CataUsuarios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CataUsuarios_Catas_CataId",
                        column: x => x.CataId,
                        principalTable: "Catas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CataUsuarios_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CataVinos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CataId = table.Column<int>(type: "int", nullable: false),
                    VinoId = table.Column<int>(type: "int", nullable: false),
                    Orden = table.Column<int>(type: "int", nullable: false),
                    EstadoVotacion = table.Column<int>(type: "int", nullable: false),
                    InicioVotacionUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FinPrevistoUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CataVinos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CataVinos_Catas_CataId",
                        column: x => x.CataId,
                        principalTable: "Catas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CataVinos_Vinos_VinoId",
                        column: x => x.VinoId,
                        principalTable: "Vinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Votos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CataId = table.Column<int>(type: "int", nullable: false),
                    VinoId = table.Column<int>(type: "int", nullable: false),
                    UsuarioId = table.Column<int>(type: "int", nullable: false),
                    Nota = table.Column<decimal>(type: "decimal(4,1)", nullable: true),
                    Comentario = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SegundosEnVotar = table.Column<int>(type: "int", nullable: false),
                    FechaVotoUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FueRectificado = table.Column<bool>(type: "bit", nullable: false),
                    EsAutomatico = table.Column<bool>(type: "bit", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Votos_Catas_CataId",
                        column: x => x.CataId,
                        principalTable: "Catas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Votos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Votos_Vinos_VinoId",
                        column: x => x.VinoId,
                        principalTable: "Vinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Catas_AdminControlaId",
                table: "Catas",
                column: "AdminControlaId");

            migrationBuilder.CreateIndex(
                name: "IX_Catas_Fecha",
                table: "Catas",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_Catas_VinoActualId",
                table: "Catas",
                column: "VinoActualId");

            migrationBuilder.CreateIndex(
                name: "IX_CataUsuarios_Cata_Usuario",
                table: "CataUsuarios",
                columns: new[] { "CataId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CataUsuarios_UsuarioId",
                table: "CataUsuarios",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_CataVinos_Cata_Orden",
                table: "CataVinos",
                columns: new[] { "CataId", "Orden" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CataVinos_Cata_Vino",
                table: "CataVinos",
                columns: new[] { "CataId", "VinoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CataVinos_VinoId",
                table: "CataVinos",
                column: "VinoId");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Login",
                table: "Usuarios",
                column: "Login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vinos_Nombre",
                table: "Vinos",
                column: "Nombre");

            migrationBuilder.CreateIndex(
                name: "IX_Votos_Cata_Vino_Usuario",
                table: "Votos",
                columns: new[] { "CataId", "VinoId", "UsuarioId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votos_UsuarioId",
                table: "Votos",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Votos_VinoId",
                table: "Votos",
                column: "VinoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CataUsuarios");

            migrationBuilder.DropTable(
                name: "CataVinos");

            migrationBuilder.DropTable(
                name: "Configuracion");

            migrationBuilder.DropTable(
                name: "Votos");

            migrationBuilder.DropTable(
                name: "Catas");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Vinos");
        }
    }
}
