using Microsoft.EntityFrameworkCore;
using MisCatas.Application.Participacion;
using MisCatas.Application.Votacion;
using MisCatas.Domain.Enums;
using MisCatas.Infrastructure.Persistencia;

namespace MisCatas.Infrastructure.Servicios;

/// <summary>
/// Implementación de <see cref="IServicioResultados"/> sobre EF Core.
///
/// Reparte el trabajo en dos mitades bien separadas a propósito:
/// <list type="number">
///   <item>la base de datos solo LEE notas y tiempos;</item>
///   <item>quién gana lo decide <see cref="CalculadoraResultados"/>, que es
///         código puro y por tanto se puede probar sin base de datos.</item>
/// </list>
///
/// Esto importa porque el desempate del concurso es por MENOR DESVIACIÓN
/// TÍPICA, y eso no se puede expresar en SQL con una simple cláusula
/// <c>ORDER BY</c> traducible por EF Core.
/// </summary>
public class ServicioResultados : IServicioResultados
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;

    public ServicioResultados(IDbContextFactory<AppDbContext> fabrica)
    {
        _fabrica = fabrica;
    }

    public async Task<ResultadoCataDto?> ObtenerResultadoAsync(
        int cataId,
        int? usuarioId = null,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        // ÚNICA puerta por la que se destapan los datos de los vinos: la cata
        // tiene que estar FINALIZADA. Si no lo está, se devuelve null y la
        // consulta de los datos sensibles ni siquiera se llega a ejecutar.
        var cata = await db.Catas
            .AsNoTracking()
            .Where(c => c.Id == cataId && c.Estado == EstadoCata.Finalizada)
            .Select(c => new
            {
                c.Id,
                c.Nombre,
                c.Lugar,
                c.Fecha,
                c.Comentarios,
                Participantes = c.Participantes.Count
            })
            .FirstOrDefaultAsync(ct);

        if (cata is null)
        {
            return null;
        }

        var filas = await LeerFilasAsync(db, cataId, usuarioId, ct);

        // El orden lo dicta la calculadora, no el SQL: así la regla del
        // desempate por consenso es la misma aquí y en las pruebas.
        var metricas = CalculadoraResultados
            .Clasificar(filas.Select(f => f.ANotasDeVino()))
            .ToDictionary(m => m.NumeroOrden);

        var clasificacion = filas
            .Select(f => new { Fila = f, Metrica = metricas[f.Orden] })
            .OrderBy(x => x.Metrica.Posicion)
            .Select(x => new VinoReveladoDto(
                x.Metrica.Posicion,
                x.Fila.Orden,
                x.Fila.VinoId,
                x.Fila.Nombre,
                x.Fila.Anio,
                x.Fila.Clase,
                x.Fila.Bodega,
                x.Fila.DenominacionOrigen,
                x.Fila.Dueno,
                x.Fila.PrecioEstimado,
                x.Fila.Comentarios,
                x.Fila.TieneFoto,
                x.Fila.TieneEtiqueta,
                x.Metrica.Media,
                x.Metrica.VotosContados,
                x.Fila.MiNota,
                x.Fila.MiComentario,
                x.Fila.Votos))
            .ToList();

        return new ResultadoCataDto(
            cata.Id,
            cata.Nombre,
            cata.Lugar,
            cata.Fecha,
            cata.Comentarios,
            cata.Participantes,
            clasificacion);
    }

    public async Task<IReadOnlyList<MetricasVino>> ObtenerMetricasAsync(
        int cataId,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        // Vista CIEGA: aquí no se lee ni el nombre del vino. Solo su orden, su
        // identificador y las notas. Se puede llamar con la cata en curso.
        var notas = await LeerNotasAsync(db, cataId, ct);

        return CalculadoraResultados.Clasificar(notas);
    }

    public async Task<MetricasVino?> ObtenerMetricasVinoAsync(
        int cataId,
        int numeroOrden,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var notas = await LeerNotasAsync(db, cataId, ct, numeroOrden);

        if (notas.Count == 0)
        {
            return null;
        }

        // Un solo vino no tiene ranking: se devuelven sus métricas sin posición.
        return CalculadoraResultados.Calcular(notas[0]);
    }

    /// <summary>
    /// Lee las notas válidas de los vinos de una cata. Los no-votos (nota
    /// nula, política <c>NoCuenta</c>) se descartan en el propio SQL para que
    /// no lleguen nunca a la media.
    /// </summary>
    private static async Task<List<NotasDeVino>> LeerNotasAsync(
        AppDbContext db,
        int cataId,
        CancellationToken ct,
        int? numeroOrden = null)
    {
        var consulta = db.CataVinos
            .AsNoTracking()
            .Where(cv => cv.CataId == cataId);

        if (numeroOrden is not null)
        {
            consulta = consulta.Where(cv => cv.Orden == numeroOrden);
        }

        var filas = await consulta
            .Select(cv => new
            {
                cv.Orden,
                cv.VinoId,
                Notas = cv.Cata.Votos
                    .Where(v => v.VinoId == cv.VinoId && v.Nota != null)
                    .Select(v => v.Nota!.Value)
                    .ToList(),
                // El tiempo medio solo tiene sentido con votos de personas:
                // los automáticos no han tardado nada en decidirse.
                Tiempos = cv.Cata.Votos
                    .Where(v => v.VinoId == cv.VinoId && !v.EsAutomatico && v.SegundosEnVotar > 0)
                    .Select(v => v.SegundosEnVotar)
                    .ToList()
            })
            .ToListAsync(ct);

        return [.. filas.Select(f => new NotasDeVino(f.Orden, f.VinoId, f.Notas, f.Tiempos))];
    }

    /// <summary>
    /// Lee los vinos de una cata FINALIZADA con todo al descubierto. No se
    /// llama nunca si la cata sigue viva.
    /// </summary>
    private static async Task<List<FilaRevelada>> LeerFilasAsync(
        AppDbContext db,
        int cataId,
        int? usuarioId,
        CancellationToken ct)
    {
        var filas = await db.CataVinos
            .AsNoTracking()
            .Where(cv => cv.CataId == cataId)
            .Select(cv => new FilaRevelada
            {
                Orden = cv.Orden,
                VinoId = cv.VinoId,
                Nombre = cv.Vino.Nombre,
                Anio = cv.Vino.Anio,
                Clase = cv.Vino.Clase,
                Bodega = cv.Vino.Bodega,
                DenominacionOrigen = cv.Vino.DenominacionOrigen,
                Dueno = cv.Vino.Dueno,
                PrecioEstimado = cv.Vino.PrecioEstimado,
                Comentarios = cv.Vino.Comentarios,
                TieneFoto = cv.Vino.Foto != null,
                TieneEtiqueta = cv.Vino.Etiqueta != null,
                MiNota = cv.Cata.Votos
                    .Where(v => v.VinoId == cv.VinoId && usuarioId != null && v.UsuarioId == usuarioId)
                    .Select(v => v.Nota)
                    .FirstOrDefault(),
                MiComentario = cv.Cata.Votos
                    .Where(v => v.VinoId == cv.VinoId && usuarioId != null && v.UsuarioId == usuarioId)
                    .Select(v => v.Comentario)
                    .FirstOrDefault(),
                Votos = cv.Cata.Votos
                    .Where(v => v.VinoId == cv.VinoId)
                    .OrderByDescending(v => v.Nota ?? decimal.MinValue)
                    .ThenBy(v => v.Usuario.Nombre)
                    .ThenBy(v => v.Usuario.Apellidos)
                    .Select(v => new VotoReveladoDto(
                        v.UsuarioId,
                        (v.Usuario.Nombre + " " + v.Usuario.Apellidos).Trim(),
                        v.Nota,
                        v.Comentario,
                        v.SegundosEnVotar,
                        v.FueRectificado,
                        v.EsAutomatico))
                    .ToList()
            })
            .ToListAsync(ct);

        return filas;
    }

    /// <summary>
    /// Fila intermedia de un vino destapado. Es una clase y no un record
    /// porque EF Core necesita poder construirla con inicializador de objeto.
    /// </summary>
    private sealed class FilaRevelada
    {
        public int Orden { get; init; }
        public int VinoId { get; init; }
        public string Nombre { get; init; } = string.Empty;
        public int? Anio { get; init; }
        public string? Clase { get; init; }
        public string? Bodega { get; init; }
        public string? DenominacionOrigen { get; init; }
        public string Dueno { get; init; } = string.Empty;
        public decimal? PrecioEstimado { get; init; }
        public string? Comentarios { get; init; }
        public bool TieneFoto { get; init; }
        public bool TieneEtiqueta { get; init; }
        public decimal? MiNota { get; init; }
        public string? MiComentario { get; init; }
        public List<VotoReveladoDto> Votos { get; init; } = [];

        /// <summary>Extrae lo que necesita la calculadora: notas y tiempos.</summary>
        public NotasDeVino ANotasDeVino() => new(
            Orden,
            VinoId,
            [.. Votos.Where(v => v.Nota is not null).Select(v => v.Nota!.Value)],
            [.. Votos.Where(v => !v.EsAutomatico && v.SegundosEnVotar > 0).Select(v => v.SegundosEnVotar)]);
    }
}
