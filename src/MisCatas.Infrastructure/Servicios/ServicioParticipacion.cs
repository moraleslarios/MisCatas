using Microsoft.EntityFrameworkCore;
using MisCatas.Application.Configuraciones;
using MisCatas.Application.Participacion;
using MisCatas.Application.Votacion;
using MisCatas.Domain.Enums;
using MisCatas.Infrastructure.Persistencia;

namespace MisCatas.Infrastructure.Servicios;

/// <summary>
/// Implementación de <see cref="IServicioParticipacion"/> sobre EF Core.
///
/// Aquí se aplica la regla de la cata ciega. Todas las proyecciones se hacen
/// con <c>Select</c> dentro de la consulta, de modo que de la base de datos
/// nunca se trae el nombre ni la foto de un vino cuya cata siga viva: lo que
/// no se lee no se puede filtrar por error más adelante.
/// </summary>
public class ServicioParticipacion : IServicioParticipacion
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;
    private readonly IServicioConfiguracion _configuracion;
    private readonly IServicioResultados _resultados;

    public ServicioParticipacion(
        IDbContextFactory<AppDbContext> fabrica,
        IServicioConfiguracion configuracion,
        IServicioResultados resultados)
    {
        _fabrica = fabrica;
        _configuracion = configuracion;
        _resultados = resultados;
    }

    public async Task<MisCatasDto> ObtenerMisCatasAsync(int usuarioId, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        // Regla dura: los roles Admin y Escaparate no catan. No se les invita a
        // ninguna cata, así que no pueden tener nada pendiente ni histórico.
        // Se corta aquí, en el servidor, y no en la pantalla.
        var rol = await db.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioId)
            .Select(u => (Rol?)u.Rol)
            .FirstOrDefaultAsync(ct);

        if (rol is null || rol is Rol.Admin or Rol.Escaparate)
        {
            return MisCatasDto.Vacio;
        }

        // Solo las catas en las que este usuario está invitado.
        var catas = await db.Catas
            .AsNoTracking()
            .Where(c => c.Participantes.Any(p => p.UsuarioId == usuarioId))
            .OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Id)
            .Select(c => new CataUsuarioDto(
                c.Id,
                c.Nombre,
                c.Lugar,
                c.Fecha,
                c.Estado,
                c.Vinos.Count,
                c.Participantes.Count,
                c.Votos.Count(v => v.UsuarioId == usuarioId)))
            .ToListAsync(ct);

        var enCurso = catas
            .Where(c => c.Estado == EstadoCata.EnCurso)
            .OrderBy(c => c.Fecha)
            .ToList();

        // «Próximas» son las que aún no han arrancado: borrador y abiertas.
        // Se ordenan de la más cercana a la más lejana.
        var proximas = catas
            .Where(c => c.Estado is EstadoCata.Borrador or EstadoCata.Abierta)
            .OrderBy(c => c.Fecha).ThenBy(c => c.Nombre)
            .ToList();

        var historico = catas
            .Where(c => c.Estado == EstadoCata.Finalizada)
            .OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Id)
            .ToList();

        return new MisCatasDto(enCurso, proximas, historico);
    }

    public async Task<CataCiegaDto?> ObtenerCataCiegaAsync(
        int cataId,
        int usuarioId,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas
            .AsNoTracking()
            .Where(c => c.Id == cataId && c.Participantes.Any(p => p.UsuarioId == usuarioId))
            .Select(c => new
            {
                c.Id,
                c.Nombre,
                c.Lugar,
                c.Fecha,
                c.Estado,
                c.TiempoVotoSegundos,
                c.VinoActualId
            })
            .FirstOrDefaultAsync(ct);

        // Si no existe o no es participante, se responde igual: null. Así no
        // se filtra ni la existencia de la cata.
        if (cata is null) return null;

        // OJO: de CataVino solo se leen Orden y estado. Nada de Vino.Nombre.
        var vinos = await db.CataVinos
            .AsNoTracking()
            .Where(cv => cv.CataId == cataId)
            .OrderBy(cv => cv.Orden)
            .Select(cv => new
            {
                cv.Id,
                cv.VinoId,
                cv.Orden,
                cv.EstadoVotacion,
                MiVoto = cv.Cata.Votos
                    .Where(v => v.UsuarioId == usuarioId && v.VinoId == cv.VinoId)
                    .Select(v => new { v.Nota, v.Comentario })
                    .FirstOrDefault(),
                // La cata está finalizada, así que ya se puede destapar quién
                // votó qué. Antes de finalizar esta consulta ni se alcanza.
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

        var vinosCiegos = vinos
            .Select(v => new VinoCiegoDto(
                v.Id,
                v.Orden,
                v.EstadoVotacion,
                v.MiVoto is not null,
                v.MiVoto?.Nota,
                v.MiVoto?.Comentario))
            .ToList();

        var progreso = await CalcularProgresoAsync(db, cataId, cata.VinoActualId, ct);
        var ajustes = await _configuracion.ObtenerAsync(ct);

        var ordenActual = cata.VinoActualId is null
            ? null
            : vinos.FirstOrDefault(v => v.VinoId == cata.VinoActualId)?.Orden;

        var estadoActual = cata.VinoActualId is null
            ? null
            : vinos.FirstOrDefault(v => v.VinoId == cata.VinoActualId)?.EstadoVotacion;

        return new CataCiegaDto(
            cata.Id,
            cata.Nombre,
            cata.Lugar,
            cata.Fecha,
            cata.Estado,
            cata.TiempoVotoSegundos ?? ajustes.TiempoVotoSegundos,
            ajustes.PasoDecimalNota,
            ordenActual,
            estadoActual,
            vinosCiegos,
            progreso);
    }

    public async Task<ProgresoCataDto?> ObtenerProgresoAsync(int cataId, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas
            .AsNoTracking()
            .Where(c => c.Id == cataId)
            .Select(c => new { c.Id, c.VinoActualId })
            .FirstOrDefaultAsync(ct);

        if (cata is null) return null;

        return await CalcularProgresoAsync(db, cataId, cata.VinoActualId, ct);
    }

    /// <summary>
    /// El resultado NO se calcula aquí: se delega en
    /// <see cref="IServicioResultados"/>, que es el único que decide quién
    /// gana. Así la clasificación que ve el participante es exactamente la
    /// misma que proyecta el escaparate, desempate por consenso incluido.
    /// </summary>
    public Task<ResultadoCataDto?> ObtenerResultadoAsync(
        int cataId,
        int? usuarioId = null,
        CancellationToken ct = default) =>
        _resultados.ObtenerResultadoAsync(cataId, usuarioId, ct);

    public async Task<CataEscaparateDto> ObtenerEscaparateAsync(CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        var ajustes = await _configuracion.ObtenerAsync(ct);

        // 1) ¿Hay alguna cata en curso? Esa manda.
        var enCurso = await db.Catas
            .AsNoTracking()
            .Where(c => c.Estado == EstadoCata.EnCurso)
            .OrderBy(c => c.FechaApertura ?? DateTime.MaxValue)
            .Select(c => new
            {
                c.Id,
                c.Nombre,
                c.Lugar,
                c.Fecha,
                c.VinoActualId,
                c.TiempoVotoSegundos,
                TotalVinos = c.Vinos.Count
            })
            .FirstOrDefaultAsync(ct);

        if (enCurso is not null)
        {
            var progreso = await CalcularProgresoAsync(db, enCurso.Id, enCurso.VinoActualId, ct);

            var pantalla = progreso.EstadoVinoActual == EstadoVotacion.Abierta
                ? PantallaEscaparate.Votacion
                : PantallaEscaparate.Resumen;

            return new CataEscaparateDto(
                pantalla,
                enCurso.Id,
                enCurso.Nombre,
                enCurso.Lugar,
                enCurso.Fecha,
                progreso.NumeroVinoActual,
                enCurso.TotalVinos,
                progreso,
                null,
                null,
                enCurso.TiempoVotoSegundos ?? ajustes.TiempoVotoSegundos,
                ajustes.PorcentajeAlertaRoja);
        }

        // 2) Si no hay nada en curso, hay dos cosas que podrían merecer la
        //    pantalla: el veredicto de una cata que acaba de terminar, o el
        //    anuncio de una cata que está a punto de empezar.
        //
        //    Se busca primero el anuncio, porque es el que manda: si el
        //    administrador ya ha abierto la cata de esta noche, la sala
        //    necesita ver cómo entrar a votar, no el podio de la cata
        //    anterior. Solo cuando no hay ninguna cata esperando se proyecta
        //    el veredicto.
        var hoy = DateTime.Today;

        var proxima = await db.Catas
            .AsNoTracking()
            .Where(c => c.Estado == EstadoCata.Abierta || c.Fecha >= hoy)
            .Where(c => c.Estado != EstadoCata.Finalizada)
            .OrderBy(c => c.Fecha).ThenBy(c => c.Id)
            .Select(c => new CataUsuarioDto(
                c.Id, c.Nombre, c.Lugar, c.Fecha, c.Estado,
                c.Vinos.Count, c.Participantes.Count, 0))
            .FirstOrDefaultAsync(ct);

        // Una cata «abierta» es una cata cuyas puertas ya están abiertas: hay
        // gente entrando con el móvil y el administrador está a un clic de
        // empezar. Esa se anuncia siempre, aunque hoy ya se haya catado antes.
        if (proxima?.Estado == EstadoCata.Abierta)
        {
            return CataEscaparateDto.EnEspera(proxima);
        }

        var finalizada = await db.Catas
            .AsNoTracking()
            .Where(c => c.Estado == EstadoCata.Finalizada && c.Fecha == hoy)
            .OrderByDescending(c => c.FechaFinalizacion ?? DateTime.MinValue)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (finalizada != 0)
        {
            var resultado = await ObtenerResultadoAsync(finalizada, null, ct);

            if (resultado is not null)
            {
                return new CataEscaparateDto(
                    PantallaEscaparate.Revelacion,
                    resultado.CataId,
                    resultado.Nombre,
                    resultado.Lugar,
                    resultado.Fecha,
                    null,
                    resultado.Clasificacion.Count,
                    null,
                    resultado,
                    // El veredicto se queda con el anuncio de la siguiente
                    // cata: así, cuando la televisión termine la gala y
                    // alguien pulse «volver al inicio», la pantalla de espera
                    // ya sabe qué hay previsto y no se queda en blanco.
                    proxima);
            }
        }

        // 3) Nada que revelar: bienvenida, anunciando lo que haya previsto.
        return CataEscaparateDto.EnEspera(proxima);
    }

    /// <summary>
    /// Métricas agregadas de una cata. Se calculan en la base de datos y solo
    /// devuelven recuentos y medias: nunca una nota individual.
    /// </summary>
    private static async Task<ProgresoCataDto> CalcularProgresoAsync(
        AppDbContext db,
        int cataId,
        int? vinoActualId,
        CancellationToken ct)
    {
        var totalParticipantes = await db.CataUsuarios
            .AsNoTracking()
            .CountAsync(cu => cu.CataId == cataId, ct);

        var vinos = await db.CataVinos
            .AsNoTracking()
            .Where(cv => cv.CataId == cataId)
            .Select(cv => new { cv.VinoId, cv.Orden, cv.EstadoVotacion })
            .ToListAsync(ct);

        var totalVinos = vinos.Count;
        var cerrados = vinos.Count(v => v.EstadoVotacion == EstadoVotacion.Cerrada);

        var actual = vinoActualId is null
            ? null
            : vinos.FirstOrDefault(v => v.VinoId == vinoActualId);

        int hanVotado = 0;
        double? tiempoMedio = null;
        Dictionary<int, int> tiemposPorUsuario = [];

        if (vinoActualId is not null)
        {
            var votos = await db.Votos
                .AsNoTracking()
                .Where(v => v.CataId == cataId && v.VinoId == vinoActualId)
                .Select(v => new { v.UsuarioId, v.SegundosEnVotar })
                .ToListAsync(ct);

            tiemposPorUsuario = votos
                .GroupBy(v => v.UsuarioId)
                .ToDictionary(g => g.Key, g => g.Min(v => v.SegundosEnVotar));

            hanVotado = tiemposPorUsuario.Count;

            if (tiemposPorUsuario.Count > 0)
            {
                tiempoMedio = tiemposPorUsuario.Values.Average();
            }
        }

        var faltan = Math.Max(0, totalParticipantes - hanVotado);

        // Lista de invitados con su estado de voto: es lo que permite al
        // escaparate encender nombres en vez de mover un simple contador.
        var invitados = await db.CataUsuarios
            .AsNoTracking()
            .Where(cu => cu.CataId == cataId)
            .Select(cu => new { cu.UsuarioId, cu.Usuario!.Nombre })
            .ToListAsync(ct);

        var participantes = invitados
            .Select(i =>
            {
                var voto = tiemposPorUsuario.TryGetValue(i.UsuarioId, out var s) ? s : (int?)null;
                return new ParticipanteVotoDto(i.UsuarioId, i.Nombre, voto is not null, voto);
            })
            .OrderBy(p => p.HaVotado ? 0 : 1)
            .ThenBy(p => p.SegundosEnVotar ?? int.MaxValue)
            .ThenBy(p => p.Nombre)
            .ToList();

        return new ProgresoCataDto(
            totalParticipantes,
            hanVotado,
            faltan,
            tiempoMedio,
            totalVinos,
            cerrados,
            actual?.Orden,
            actual?.EstadoVotacion,
            participantes);
    }
}
