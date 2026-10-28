using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MisCatas.Application.Catas;
using MisCatas.Application.Comun;
using MisCatas.Application.Configuraciones;
using MisCatas.Application.Participacion;
using MisCatas.Application.Votacion;
using MisCatas.Domain.Entidades;
using MisCatas.Domain.Enums;
using MisCatas.Infrastructure.Persistencia;

namespace MisCatas.Infrastructure.Servicios;

/// <summary>
/// Motor de la cata en vivo.
///
/// Aquí se concentran todas las decisiones que la interfaz NO puede tomar:
/// quién gobierna la cata, cuándo se abre y se cierra una votación, cuánto
/// tiempo queda de verdad y qué se puede votar. El reloj es del servidor.
/// </summary>
public class ServicioVotacion : IServicioVotacion
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;
    private readonly IServicioConfiguracion _configuracion;
    private readonly EstadoCatasActivas _estado;
    private readonly INotificadorCata _notificador;
    private readonly ILogger<ServicioVotacion> _log;

    public ServicioVotacion(
        IDbContextFactory<AppDbContext> fabrica,
        IServicioConfiguracion configuracion,
        EstadoCatasActivas estado,
        INotificadorCata notificador,
        ILogger<ServicioVotacion> log)
    {
        _fabrica = fabrica;
        _configuracion = configuracion;
        _estado = estado;
        _notificador = notificador;
        _log = log;
    }

    // ---------------------------------------------------------------- estado

    public async Task<EstadoVivoDto?> ObtenerEstadoAsync(int cataId, CancellationToken ct = default)
    {
        if (_estado.Obtener(cataId) is { } enMemoria)
        {
            return enMemoria.AEstado(DateTime.UtcNow);
        }

        await using var db = await _fabrica.CreateDbContextAsync(ct);
        var viva = await RehidratarAsync(db, cataId, ct);
        return viva?.AEstado(DateTime.UtcNow);
    }

    /// <summary>
    /// Reconstruye la foto en memoria a partir de la base de datos. Es lo que
    /// permite que un cliente que recarga a mitad de votación recupere el
    /// tiempo restante REAL y no uno inventado por su navegador.
    /// </summary>
    public async Task<CataViva?> RehidratarAsync(AppDbContext db, int cataId, CancellationToken ct)
    {
        var cata = await db.Catas
            .AsNoTracking()
            .Where(c => c.Id == cataId)
            .Select(c => new
            {
                c.Id,
                c.Estado,
                c.TiempoVotoSegundos,
                c.RevelacionActiva,
                c.AdminControlaId,
                c.VinoActualId,
                TotalVinos = c.Vinos.Count,
            })
            .FirstOrDefaultAsync(ct);

        if (cata is null)
        {
            return null;
        }

        var ajustes = await _configuracion.ObtenerAsync(ct);
        var segundos = cata.TiempoVotoSegundos ?? ajustes.TiempoVotoSegundos;

        var actual = cata.VinoActualId is null
            ? null
            : await db.CataVinos
                .AsNoTracking()
                .Where(cv => cv.CataId == cataId && cv.VinoId == cata.VinoActualId)
                .Select(cv => new { cv.Orden, cv.EstadoVotacion, cv.InicioVotacionUtc, cv.FinPrevistoUtc, cv.SegundosPausaRestantes })
                .FirstOrDefaultAsync(ct);

        var fase = FaseVotacion.Inactiva;
        DateTime? apertura = null;
        DateTime? fin = null;
        int? pausa = null;

        if (actual is not null)
        {
            if (actual.EstadoVotacion == EstadoVotacion.Abierta)
            {
                apertura = actual.InicioVotacionUtc;
                fin = actual.FinPrevistoUtc;

                if (actual.SegundosPausaRestantes is int remanente)
                {
                    // La pausa manda sobre todo lo demás: ni cuenta atrás de
                    // preparación ni reloj corriendo mientras esté congelada.
                    pausa = remanente;
                    fase = FaseVotacion.Pausada;
                }
                else
                {
                    // Si el proceso se reinició durante la preparación, la apertura
                    // puede seguir en el futuro: se respeta tal cual.
                    fase = apertura is DateTime a && a > DateTime.UtcNow
                        ? FaseVotacion.Preparacion
                        : FaseVotacion.Abierta;
                }
            }
            else if (actual.EstadoVotacion == EstadoVotacion.Cerrada)
            {
                fase = FaseVotacion.Cerrada;
            }
        }
        else if (cata.Estado == EstadoCata.EnCurso)
        {
            fase = FaseVotacion.Cerrada;
        }

        var viva = new CataViva(
            cata.Id,
            cata.Estado,
            cata.TotalVinos,
            segundos,
            ajustes.PorcentajeAlertaRoja,
            actual?.Orden,
            fase,
            apertura,
            fin,
            AlertaEnviada: false,
            cata.RevelacionActiva,
            cata.AdminControlaId,
            SegundosPausaRestantes: pausa);

        return _estado.Guardar(viva);
    }

    // ------------------------------------------------------------ transiciones

    /// <summary>
    /// Regla dura: solo puede haber UNA cata activa (abierta o en curso) en
    /// todo el sistema. Devuelve un fallo si otra cata distinta ocupa el hueco.
    /// </summary>
    private static async Task<Resultado> ComprobarHuecoLibreAsync(
        AppDbContext db,
        int cataId,
        CancellationToken ct)
    {
        var ocupada = await db.Catas
            .AsNoTracking()
            .Where(CataActiva.Filtro)
            .Where(c => c.Id != cataId)
            .OrderByDescending(c => c.Estado)
            .Select(c => new { c.Nombre, c.Estado })
            .FirstOrDefaultAsync(ct);

        return ocupada is null
            ? Resultado.Ok()
            : Resultado.Fallo(CataActiva.MensajeOcupado(ocupada.Nombre, ocupada.Estado));
    }

    public async Task<Resultado> AbrirCataAsync(int cataId, int adminId, CancellationToken ct = default)
    {
        using var _ = await _estado.BloquearAsync(ct);
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (cata.Estado != EstadoCata.Borrador)
        {
            return Resultado.Fallo("Solo se puede abrir una cata que esté en borrador.");
        }

        if (await ComprobarHuecoLibreAsync(db, cataId, ct) is { EsFallo: true } ocupado)
        {
            return ocupado;
        }

        var vinos = await db.CataVinos.CountAsync(cv => cv.CataId == cataId, ct);
        if (vinos == 0)
        {
            return Resultado.Fallo("La cata no tiene vinos: no se puede abrir.");
        }

        var participantes = await db.CataUsuarios.CountAsync(cu => cu.CataId == cataId, ct);
        if (participantes == 0)
        {
            return Resultado.Fallo("La cata no tiene participantes: no se puede abrir.");
        }

        cata.Estado = EstadoCata.Abierta;
        cata.FechaApertura = DateTime.UtcNow;
        cata.AdminControlaId = adminId;
        cata.RevelacionActiva = false;
        await db.SaveChangesAsync(ct);

        _log.LogInformation("Cata {CataId} abierta por el administrador {AdminId}.", cataId, adminId);

        var viva = await RehidratarAsync(db, cataId, ct);
        await NotificarAsync(cataId, EventosCata.CataAbierta, viva, ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> LanzarVotacionAsync(int cataId, int numeroOrden, int adminId, CancellationToken ct = default)
    {
        using var _ = await _estado.BloquearAsync(ct);
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (ComprobarMando(cata, adminId) is { EsFallo: true } mando)
        {
            return mando;
        }

        if (cata.Estado is not (EstadoCata.Abierta or EstadoCata.EnCurso))
        {
            return Resultado.Fallo("Solo se pueden lanzar votaciones en una cata abierta o en curso.");
        }

        var destino = await db.CataVinos
            .FirstOrDefaultAsync(cv => cv.CataId == cataId && cv.Orden == numeroOrden, ct);

        if (destino is null)
        {
            return Resultado.Fallo($"La cata no tiene ningún vino en la posición {numeroOrden}.");
        }

        if (destino.EstadoVotacion == EstadoVotacion.Abierta)
        {
            return Resultado.Fallo("La votación de ese vino ya está en marcha.");
        }

        var ajustes = await _configuracion.ObtenerAsync(ct);
        var segundos = cata.TiempoVotoSegundos ?? ajustes.TiempoVotoSegundos;

        // Cierra lo que hubiera abierto: solo puede haber un vino en juego.
        var abiertos = await db.CataVinos
            .Where(cv => cv.CataId == cataId && cv.EstadoVotacion == EstadoVotacion.Abierta)
            .ToListAsync(ct);

        foreach (var abierto in abiertos)
        {
            abierto.EstadoVotacion = EstadoVotacion.Cerrada;
            await AplicarPoliticaNoVotoAsync(db, cata, abierto, ajustes, ct);
        }

        var ahora = DateTime.UtcNow;
        var apertura = ahora.AddSeconds(EstadoCatasActivas.SegundosPreparacion);

        destino.EstadoVotacion = EstadoVotacion.Abierta;
        destino.InicioVotacionUtc = apertura;      // el tiempo se mide desde el 0
        destino.FinPrevistoUtc = apertura.AddSeconds(segundos);
        destino.SegundosPausaRestantes = null;     // una votación nueva nunca nace pausada

        cata.VinoActualId = destino.VinoId;
        if (cata.Estado == EstadoCata.Abierta)
        {
            cata.Estado = EstadoCata.EnCurso;
        }

        await db.SaveChangesAsync(ct);

        _log.LogInformation(
            "Cata {CataId}: lanzada la votación del vino nº {Orden} ({Segundos} s) por {AdminId}.",
            cataId, numeroOrden, segundos, adminId);

        var viva = new CataViva(
            cataId,
            cata.Estado,
            await db.CataVinos.CountAsync(cv => cv.CataId == cataId, ct),
            segundos,
            ajustes.PorcentajeAlertaRoja,
            numeroOrden,
            FaseVotacion.Preparacion,
            apertura,
            destino.FinPrevistoUtc,
            AlertaEnviada: false,
            cata.RevelacionActiva,
            cata.AdminControlaId,
            SegundosPausaRestantes: null);

        _estado.Guardar(viva);
        await NotificarAsync(cataId, EventosCata.CuentaAtrasIniciada, viva, ct);
        await EnviarResumenAsync(cataId, ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> CerrarVotacionAsync(int cataId, int adminId, CancellationToken ct = default)
    {
        using var _ = await _estado.BloquearAsync(ct);
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (ComprobarMando(cata, adminId) is { EsFallo: true } mando)
        {
            return mando;
        }

        var cerrado = await CerrarVotacionInternaAsync(db, cata, ct);
        if (!cerrado)
        {
            return Resultado.Fallo("No hay ninguna votación abierta que cerrar.");
        }

        _log.LogInformation("Cata {CataId}: votación cerrada a mano por {AdminId}.", cataId, adminId);
        return Resultado.Ok();
    }

    // ------------------------------------------------------------ pausa/reanuda

    public async Task<Resultado> PausarVotacionAsync(int cataId, int adminId, CancellationToken ct = default)
    {
        using var _ = await _estado.BloquearAsync(ct);
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (ComprobarMando(cata, adminId) is { EsFallo: true } mandoPausa)
        {
            return mandoPausa;
        }

        var viva = _estado.Obtener(cataId) ?? await RehidratarAsync(db, cataId, ct);

        if (viva is null || viva.Fase != FaseVotacion.Abierta)
        {
            return Resultado.Fallo(
                viva?.Fase == FaseVotacion.Pausada
                    ? "La votación ya está pausada."
                    : "No hay ninguna votación abierta que pausar.");
        }

        var abiertoPausa = await db.CataVinos
            .FirstOrDefaultAsync(cv => cv.CataId == cataId && cv.EstadoVotacion == EstadoVotacion.Abierta, ct);

        if (abiertoPausa is null)
        {
            return Resultado.Fallo("No hay ninguna votación abierta que pausar.");
        }

        // Se congela el REMANENTE, no el instante de fin: así la pausa puede
        // durar lo que haga falta y hasta sobrevivir a un reinicio del proceso.
        var ahoraPausa = DateTime.UtcNow;
        var restantesPausa = abiertoPausa.FinPrevistoUtc is DateTime finPausa
            ? Math.Max(0, (int)Math.Ceiling((finPausa - ahoraPausa).TotalSeconds))
            : viva.SegundosTotales;

        if (restantesPausa <= 0)
        {
            return Resultado.Fallo("A esa votación ya no le queda tiempo: ciérrala en lugar de pausarla.");
        }

        abiertoPausa.SegundosPausaRestantes = restantesPausa;
        await db.SaveChangesAsync(ct);

        var pausada = _estado.Mutar(cataId, v => v with
        {
            Fase = FaseVotacion.Pausada,
            SegundosPausaRestantes = restantesPausa,
        }) ?? await RehidratarAsync(db, cataId, ct);

        _log.LogInformation(
            "Cata {CataId}: votación del vino nº {Orden} PAUSADA por {AdminId} con {Restantes} s restantes.",
            cataId, viva.NumeroVinoActual, adminId, restantesPausa);

        await NotificarAsync(cataId, EventosCata.VotacionPausada, pausada, ct);
        await EnviarResumenAsync(cataId, ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> ReanudarVotacionAsync(int cataId, int adminId, CancellationToken ct = default)
    {
        using var _ = await _estado.BloquearAsync(ct);
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (ComprobarMando(cata, adminId) is { EsFallo: true } mandoReanuda)
        {
            return mandoReanuda;
        }

        var enPausa = await db.CataVinos
            .FirstOrDefaultAsync(
                cv => cv.CataId == cataId
                      && cv.EstadoVotacion == EstadoVotacion.Abierta
                      && cv.SegundosPausaRestantes != null,
                ct);

        if (enPausa is null)
        {
            return Resultado.Fallo("No hay ninguna votación pausada que reanudar.");
        }

        var restantesReanuda = Math.Max(1, enPausa.SegundosPausaRestantes!.Value);
        var ahoraReanuda = DateTime.UtcNow;
        var finNuevo = ahoraReanuda.AddSeconds(restantesReanuda);

        // El cronómetro de cada voto se mide desde InicioVotacionUtc, así que
        // hay que desplazarlo tanto como haya durado la pausa para no inflar
        // los segundos que tardó cada uno en decidirse.
        var consumidos = enPausa.FinPrevistoUtc is DateTime finAnterior && enPausa.InicioVotacionUtc is DateTime inicio
            ? (finAnterior - inicio).TotalSeconds - restantesReanuda
            : 0d;

        if (consumidos >= 0d)
        {
            enPausa.InicioVotacionUtc = ahoraReanuda.AddSeconds(-consumidos);
        }

        enPausa.FinPrevistoUtc = finNuevo;
        enPausa.SegundosPausaRestantes = null;
        await db.SaveChangesAsync(ct);

        var reanudada = _estado.Mutar(cataId, v => v with
        {
            Fase = FaseVotacion.Abierta,
            AperturaPrevistaUtc = enPausa.InicioVotacionUtc,
            FinPrevistoUtc = finNuevo,
            SegundosPausaRestantes = null,
            AlertaEnviada = false,
        }) ?? await RehidratarAsync(db, cataId, ct);

        _log.LogInformation(
            "Cata {CataId}: votación REANUDADA por {AdminId} con {Restantes} s.",
            cataId, adminId, restantesReanuda);

        await NotificarAsync(cataId, EventosCata.VotacionReanudada, reanudada, ct);
        await EnviarResumenAsync(cataId, ct);
        return Resultado.Ok();
    }

    /// <summary>
    /// Cierra la votación en curso, aplica la política de no-voto y avisa a
    /// todo el mundo. La usan tanto el administrador como el reloj del
    /// servidor cuando se agota el tiempo.
    /// </summary>
    public async Task<bool> CerrarVotacionInternaAsync(AppDbContext db, Cata cata, CancellationToken ct)
    {
        var abierto = await db.CataVinos
            .FirstOrDefaultAsync(cv => cv.CataId == cata.Id && cv.EstadoVotacion == EstadoVotacion.Abierta, ct);

        if (abierto is null)
        {
            return false;
        }

        var ajustes = await _configuracion.ObtenerAsync(ct);

        abierto.EstadoVotacion = EstadoVotacion.Cerrada;
        abierto.SegundosPausaRestantes = null;   // cerrar también deshace la pausa
        await AplicarPoliticaNoVotoAsync(db, cata, abierto, ajustes, ct);
        await db.SaveChangesAsync(ct);

        var viva = _estado.Mutar(cata.Id, v => v with
        {
            Fase = FaseVotacion.Cerrada,
            AlertaEnviada = false,
            SegundosPausaRestantes = null,
        }) ?? await RehidratarAsync(db, cata.Id, ct);

        await NotificarAsync(cata.Id, EventosCata.VotacionCerrada, viva, ct);
        await EnviarResumenAsync(cata.Id, ct);
        return true;
    }

    public async Task<Resultado> FinalizarCataAsync(int cataId, int adminId, CancellationToken ct = default)
    {
        using var _ = await _estado.BloquearAsync(ct);
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (ComprobarMando(cata, adminId) is { EsFallo: true } mando)
        {
            return mando;
        }

        if (!TransicionesCata.EsValida(cata.Estado, EstadoCata.Finalizada))
        {
            return Resultado.Fallo("Esta cata no se puede finalizar desde su estado actual.");
        }

        var ajustes = await _configuracion.ObtenerAsync(ct);

        // Cierra lo que quede abierto antes de destapar nada.
        var abiertos = await db.CataVinos
            .Where(cv => cv.CataId == cataId && cv.EstadoVotacion == EstadoVotacion.Abierta)
            .ToListAsync(ct);

        foreach (var abierto in abiertos)
        {
            abierto.EstadoVotacion = EstadoVotacion.Cerrada;
            await AplicarPoliticaNoVotoAsync(db, cata, abierto, ajustes, ct);
        }

        cata.Estado = EstadoCata.Finalizada;
        cata.FechaFinalizacion = DateTime.UtcNow;
        cata.VinoActualId = null;
        cata.RevelacionActiva = true;
        await db.SaveChangesAsync(ct);

        _log.LogInformation("Cata {CataId} finalizada por {AdminId}.", cataId, adminId);

        var viva = await RehidratarAsync(db, cataId, ct);
        await NotificarAsync(cataId, EventosCata.CataFinalizada, viva, ct);
        await NotificarAsync(cataId, EventosCata.RevelacionIniciada, viva, ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> ReabrirCataAsync(int cataId, int adminId, CancellationToken ct = default)
    {
        using var _ = await _estado.BloquearAsync(ct);
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (ComprobarMando(cata, adminId) is { EsFallo: true } mando)
        {
            return mando;
        }

        if (!TransicionesCata.EsValida(cata.Estado, EstadoCata.EnCurso))
        {
            return Resultado.Fallo("Solo se puede reabrir una cata finalizada.");
        }

        if (await ComprobarHuecoLibreAsync(db, cataId, ct) is { EsFallo: true } ocupadoReabrir)
        {
            return ocupadoReabrir;
        }

        cata.Estado = EstadoCata.EnCurso;
        cata.FechaFinalizacion = null;
        cata.RevelacionActiva = false;
        await db.SaveChangesAsync(ct);

        _log.LogInformation("Cata {CataId} reabierta por {AdminId}: los datos se vuelven a ocultar.", cataId, adminId);

        var viva = await RehidratarAsync(db, cataId, ct);
        await NotificarAsync(cataId, EventosCata.CataReabierta, viva, ct);
        return Resultado.Ok();
    }

    public Task<Resultado> ActivarRevelacionAsync(int cataId, int adminId, CancellationToken ct = default) =>
        CambiarRevelacionAsync(cataId, adminId, activa: true, ct);

    public Task<Resultado> OcultarRevelacionAsync(int cataId, int adminId, CancellationToken ct = default) =>
        CambiarRevelacionAsync(cataId, adminId, activa: false, ct);

    private async Task<Resultado> CambiarRevelacionAsync(int cataId, int adminId, bool activa, CancellationToken ct)
    {
        using var _ = await _estado.BloquearAsync(ct);
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (ComprobarMando(cata, adminId) is { EsFallo: true } mando)
        {
            return mando;
        }

        if (activa && cata.Estado != EstadoCata.Finalizada)
        {
            return Resultado.Fallo("La revelación solo se puede lanzar con la cata finalizada.");
        }

        cata.RevelacionActiva = activa;
        await db.SaveChangesAsync(ct);

        var viva = _estado.Mutar(cataId, v => v with { RevelacionActiva = activa })
            ?? await RehidratarAsync(db, cataId, ct);

        var evento = activa ? EventosCata.RevelacionIniciada : EventosCata.RevelacionOcultada;
        _log.LogInformation("Cata {CataId}: {Evento} por {AdminId}.", cataId, evento, adminId);
        await NotificarAsync(cataId, evento, viva, ct);
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------ votos

    public async Task<Resultado<MiVotoDto>> VotarAsync(
        int cataId,
        int numeroOrden,
        int usuarioId,
        decimal nota,
        string? comentario,
        CancellationToken ct = default)
    {
        using var _ = await _estado.BloquearAsync(ct);
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas
            .AsNoTracking()
            .Where(c => c.Id == cataId)
            .Select(c => new { c.Id, c.Estado })
            .FirstOrDefaultAsync(ct);

        if (cata is null)
        {
            return Resultado.Fallo<MiVotoDto>("La cata no existe.");
        }

        if (cata.Estado != EstadoCata.EnCurso)
        {
            return Resultado.Fallo<MiVotoDto>("Esta cata no está en curso: no se pueden registrar votos.");
        }

        var esParticipante = await db.CataUsuarios
            .AnyAsync(cu => cu.CataId == cataId && cu.UsuarioId == usuarioId, ct);

        if (!esParticipante)
        {
            return Resultado.Fallo<MiVotoDto>("No participas en esta cata.");
        }

        var ajustes = await _configuracion.ObtenerAsync(ct);
        if (ValidarNota(nota, ajustes.PasoDecimalNota) is { } errorNota)
        {
            return Resultado.Fallo<MiVotoDto>(errorNota);
        }

        var cataVino = await db.CataVinos
            .AsNoTracking()
            .Where(cv => cv.CataId == cataId && cv.Orden == numeroOrden)
            .Select(cv => new { cv.VinoId, cv.EstadoVotacion, cv.InicioVotacionUtc })
            .FirstOrDefaultAsync(ct);

        if (cataVino is null)
        {
            return Resultado.Fallo<MiVotoDto>($"La cata no tiene ningún vino en la posición {numeroOrden}.");
        }

        var viva = _estado.Obtener(cataId) ?? await RehidratarAsync(db, cataId, ct);
        var ahora = DateTime.UtcNow;
        var esVinoEnJuego = viva?.NumeroVinoActual == numeroOrden;

        // Dos ventanas legítimas: la votación abierta del vino en juego y la
        // rectificación de un vino ya cerrado entre votaciones.
        var votacionAbierta = esVinoEnJuego
            && viva?.Fase == FaseVotacion.Abierta
            && viva.FinPrevistoUtc is DateTime fin
            && fin > ahora;

        var rectificando = !esVinoEnJuego
            && cataVino.EstadoVotacion == EstadoVotacion.Cerrada
            && viva is not null
            && viva.Fase is FaseVotacion.Cerrada or FaseVotacion.Inactiva;

        if (!votacionAbierta && !rectificando)
        {
            return Resultado.Fallo<MiVotoDto>(
                (esVinoEnJuego ? viva?.Fase : null) switch
                {
                    FaseVotacion.Preparacion => "La votación todavía no está abierta: espera a que termine la cuenta atrás.",
                    FaseVotacion.Pausada => "El administrador ha pausado la votación: espera a que la reanude.",
                    _ => "La votación de este vino está cerrada en este momento.",
                });
        }

        var texto = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();

        // Upsert: el índice único (CataId, VinoId, UsuarioId) evita duplicados
        // por doble clic o doble pestaña, así que siempre buscamos primero.
        var voto = await db.Votos.FirstOrDefaultAsync(
            v => v.CataId == cataId && v.VinoId == cataVino.VinoId && v.UsuarioId == usuarioId, ct);

        var nuevo = voto is null;
        if (nuevo)
        {
            voto = new Voto
            {
                CataId = cataId,
                VinoId = cataVino.VinoId,
                UsuarioId = usuarioId,
            };
            db.Votos.Add(voto);
        }

        var teniaNota = !nuevo && voto!.Nota is not null && !voto.EsAutomatico;

        voto!.Nota = nota;
        voto.Comentario = texto;
        voto.FechaVotoUtc = ahora;
        voto.EsAutomatico = false;
        voto.FueRectificado = teniaNota;

        if (votacionAbierta && !teniaNota && cataVino.InicioVotacionUtc is DateTime arranque)
        {
            // Se mide desde que la votación SE ABRE (tras el cero), no desde
            // que el administrador lanzó la cuenta atrás de preparación.
            voto.SegundosEnVotar = Math.Max(0, (int)Math.Round((ahora - arranque).TotalSeconds));
        }

        await db.SaveChangesAsync(ct);

        await EnviarResumenAsync(cataId, ct);

        return Resultado.Ok(new MiVotoDto(
            numeroOrden,
            voto.Nota,
            voto.Comentario,
            voto.SegundosEnVotar,
            voto.FueRectificado));
    }

    public async Task<Resultado> ResetearVotoUsuarioAsync(
        int cataId, int numeroOrden, int usuarioId, int adminId, CancellationToken ct = default)
    {
        using var _ = await _estado.BloquearAsync(ct);
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (ComprobarMando(cata, adminId) is { EsFallo: true } mando)
        {
            return mando;
        }

        var vinoId = await db.CataVinos
            .Where(cv => cv.CataId == cataId && cv.Orden == numeroOrden)
            .Select(cv => (int?)cv.VinoId)
            .FirstOrDefaultAsync(ct);

        if (vinoId is null)
        {
            return Resultado.Fallo($"La cata no tiene ningún vino en la posición {numeroOrden}.");
        }

        var borrados = await db.Votos
            .Where(v => v.CataId == cataId && v.VinoId == vinoId && v.UsuarioId == usuarioId)
            .ExecuteDeleteAsync(ct);

        if (borrados == 0)
        {
            return Resultado.Fallo("Ese participante no tenía voto en este vino.");
        }

        _log.LogWarning(
            "Cata {CataId}: {AdminId} ha borrado el voto del usuario {UsuarioId} en el vino nº {Orden}.",
            cataId, adminId, usuarioId, numeroOrden);

        await NotificarAsync(cataId, EventosCata.VotoReseteado, _estado.Obtener(cataId), ct);
        await EnviarResumenAsync(cataId, ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> ResetearVinoAsync(int cataId, int numeroOrden, int adminId, CancellationToken ct = default)
    {
        using var _ = await _estado.BloquearAsync(ct);
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (ComprobarMando(cata, adminId) is { EsFallo: true } mando)
        {
            return mando;
        }

        var cataVino = await db.CataVinos
            .FirstOrDefaultAsync(cv => cv.CataId == cataId && cv.Orden == numeroOrden, ct);

        if (cataVino is null)
        {
            return Resultado.Fallo($"La cata no tiene ningún vino en la posición {numeroOrden}.");
        }

        await db.Votos
            .Where(v => v.CataId == cataId && v.VinoId == cataVino.VinoId)
            .ExecuteDeleteAsync(ct);

        cataVino.EstadoVotacion = EstadoVotacion.Pendiente;
        cataVino.InicioVotacionUtc = null;
        cataVino.FinPrevistoUtc = null;

        if (cata.VinoActualId == cataVino.VinoId)
        {
            cata.VinoActualId = null;
        }

        await db.SaveChangesAsync(ct);

        _log.LogWarning(
            "Cata {CataId}: {AdminId} ha borrado TODOS los votos del vino nº {Orden}.",
            cataId, adminId, numeroOrden);

        var viva = await RehidratarAsync(db, cataId, ct);
        await NotificarAsync(cataId, EventosCata.VotoReseteado, viva, ct);
        await EnviarResumenAsync(cataId, ct);
        return Resultado.Ok();
    }

    // --------------------------------------------------------------- resumen

    public async Task<ProgresoCataDto> ObtenerResumenAsync(int cataId, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        return await CalcularResumenAsync(db, cataId, ct);
    }

    /// <summary>
    /// Progreso agregado: solo métricas, nunca datos del vino ni notas. Es lo
    /// que ven los usuarios y las televisiones mientras la cata sigue viva.
    /// </summary>
    private static async Task<ProgresoCataDto> CalcularResumenAsync(AppDbContext db, int cataId, CancellationToken ct)
    {
        var cata = await db.Catas
            .AsNoTracking()
            .Where(c => c.Id == cataId)
            .Select(c => new { c.VinoActualId })
            .FirstOrDefaultAsync(ct);

        var totalParticipantes = await db.CataUsuarios.CountAsync(cu => cu.CataId == cataId, ct);

        var vinos = await db.CataVinos
            .AsNoTracking()
            .Where(cv => cv.CataId == cataId)
            .Select(cv => new { cv.VinoId, cv.Orden, cv.EstadoVotacion })
            .ToListAsync(ct);

        var cerrados = vinos.Count(v => v.EstadoVotacion == EstadoVotacion.Cerrada);
        var actual = cata?.VinoActualId is null
            ? null
            : vinos.FirstOrDefault(v => v.VinoId == cata.VinoActualId);

        var hanVotado = 0;
        double? tiempoMedio = null;
        Dictionary<int, int> tiemposPorUsuario = [];

        if (actual is not null)
        {
            var votos = await db.Votos
                .AsNoTracking()
                .Where(v => v.CataId == cataId && v.VinoId == actual.VinoId && !v.EsAutomatico)
                .Select(v => new { v.UsuarioId, v.SegundosEnVotar })
                .ToListAsync(ct);

            tiemposPorUsuario = votos
                .GroupBy(v => v.UsuarioId)
                .ToDictionary(g => g.Key, g => g.Min(v => v.SegundosEnVotar));

            hanVotado = tiemposPorUsuario.Count;
            tiempoMedio = tiemposPorUsuario.Count == 0
                ? null
                : tiemposPorUsuario.Values.Average();
        }

        // La sala quiere ver NOMBRES encendiéndose, no un contador: se devuelve
        // la lista completa de invitados con su estado de voto. Nunca la nota.
        var participantes = await ListarParticipantesAsync(db, cataId, tiemposPorUsuario, ct);

        return new ProgresoCataDto(
            totalParticipantes,
            hanVotado,
            Math.Max(0, totalParticipantes - hanVotado),
            tiempoMedio,
            vinos.Count,
            cerrados,
            actual?.Orden,
            actual?.EstadoVotacion,
            participantes);
    }

    /// <summary>
    /// Invitados de la cata con su estado de voto en el vino en juego. Se
    /// ordena por tiempo de voto ascendente —primero quien antes votó— y
    /// después por nombre, de modo que la televisión cuenta por sí sola la
    /// carrera del vino.
    /// </summary>
    private static async Task<List<ParticipanteVotoDto>> ListarParticipantesAsync(
        AppDbContext db,
        int cataId,
        Dictionary<int, int> tiemposPorUsuario,
        CancellationToken ct)
    {
        var invitados = await db.CataUsuarios
            .AsNoTracking()
            .Where(cu => cu.CataId == cataId)
            .Select(cu => new { cu.UsuarioId, cu.Usuario!.Nombre })
            .ToListAsync(ct);

        return [.. invitados
            .Select(i =>
            {
                var voto = tiemposPorUsuario.TryGetValue(i.UsuarioId, out var s) ? s : (int?)null;
                return new ParticipanteVotoDto(i.UsuarioId, i.Nombre, voto is not null, voto);
            })
            .OrderBy(p => p.HaVotado ? 0 : 1)
            .ThenBy(p => p.SegundosEnVotar ?? int.MaxValue)
            .ThenBy(p => p.Nombre)];
    }

    private async Task EnviarResumenAsync(int cataId, CancellationToken ct)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);
        var progreso = await CalcularResumenAsync(db, cataId, ct);
        await _notificador.EnviarResumenAsync(cataId, progreso, ct);
    }

    private async Task NotificarAsync(int cataId, string evento, CataViva? viva, CancellationToken ct)
    {
        if (viva is null)
        {
            return;
        }

        await _notificador.EnviarEstadoAsync(cataId, evento, viva.AEstado(DateTime.UtcNow), ct);
    }

    // ----------------------------------------------------------- validaciones

    /// <summary>
    /// Solo gobierna la cata el administrador que la abrió. Los demás la ven,
    /// pero en modo lectura.
    /// </summary>
    private static Resultado ComprobarMando(Cata cata, int adminId) =>
        cata.AdminControlaId is null || cata.AdminControlaId == adminId
            ? Resultado.Ok()
            : Resultado.Fallo("Esta cata la gobierna otro administrador: tú la ves en modo lectura.");

    private static string? ValidarNota(decimal nota, decimal paso)
    {
        if (nota < 0m || nota > 10m)
        {
            return "La nota tiene que estar entre 0 y 10.";
        }

        if (paso > 0m && nota % paso != 0m)
        {
            return $"La nota debe ir de {paso:0.##} en {paso:0.##}.";
        }

        return null;
    }

    /// <summary>
    /// Al cerrar una votación, los participantes que no votaron reciben un
    /// registro automático. Con la política por defecto (NoCuenta) la nota
    /// queda vacía y NO entra en la media: un olvido no debe hundir un vino.
    /// </summary>
    private static async Task AplicarPoliticaNoVotoAsync(
        AppDbContext db,
        Cata cata,
        CataVino cataVino,
        Configuracion ajustes,
        CancellationToken ct)
    {
        var participantes = await db.CataUsuarios
            .Where(cu => cu.CataId == cata.Id)
            .Select(cu => cu.UsuarioId)
            .ToListAsync(ct);

        var yaVotaron = await db.Votos
            .Where(v => v.CataId == cata.Id && v.VinoId == cataVino.VinoId)
            .Select(v => v.UsuarioId)
            .ToListAsync(ct);

        var faltan = participantes.Except(yaVotaron).ToList();
        if (faltan.Count == 0)
        {
            return;
        }

        var nota = ajustes.PoliticaNoVoto == PoliticaNoVoto.NotaFija
            ? ajustes.NotaPorDefectoNoVoto
            : null;

        foreach (var usuarioId in faltan)
        {
            db.Votos.Add(new Voto
            {
                CataId = cata.Id,
                VinoId = cataVino.VinoId,
                UsuarioId = usuarioId,
                Nota = nota,
                Comentario = ajustes.ComentarioAutomaticoNoVoto,
                SegundosEnVotar = 0,
                FechaVotoUtc = DateTime.UtcNow,
                EsAutomatico = true,
            });
        }
    }
}
