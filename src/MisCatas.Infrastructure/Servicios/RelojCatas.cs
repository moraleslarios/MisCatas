using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MisCatas.Application.Votacion;
using MisCatas.Domain.Enums;
using MisCatas.Infrastructure.Persistencia;

namespace MisCatas.Infrastructure.Servicios;

/// <summary>
/// Reloj autoritativo del servidor.
///
/// Es la pieza que impide que cada móvil muestre un tiempo distinto: ningún
/// navegador cuenta por su cuenta. Este servicio recorre las catas vivas cada
/// pocos cientos de milisegundos y es el ÚNICO que decide cuándo se abre la
/// votación tras la cuenta atrás de preparación y cuándo se cierra por tiempo.
/// </summary>
public sealed class RelojCatas : BackgroundService
{
    /// <summary>Cadencia del tick. Suficiente para que la cuenta se vea fluida.</summary>
    private static readonly TimeSpan Cadencia = TimeSpan.FromMilliseconds(400);

    private readonly EstadoCatasActivas _estado;
    private readonly INotificadorCata _notificador;
    private readonly IServiceScopeFactory _ambitos;
    private readonly ILogger<RelojCatas> _log;

    public RelojCatas(
        EstadoCatasActivas estado,
        INotificadorCata notificador,
        IServiceScopeFactory ambitos,
        ILogger<RelojCatas> log)
    {
        _estado = estado;
        _notificador = notificador;
        _ambitos = ambitos;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await RehidratarCatasVivasAsync(ct);

        using var timer = new PeriodicTimer(Cadencia);

        while (await EsperarAsync(timer, ct))
        {
            try
            {
                await LatirAsync(ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                // Un fallo puntual (base de datos ocupada, cliente caído) no
                // puede parar el reloj en mitad de una cata.
                _log.LogError(ex, "Fallo en el tick del reloj de catas.");
            }
        }
    }

    private static async Task<bool> EsperarAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    /// <summary>
    /// Al arrancar el proceso vuelve a cargar en memoria las catas que estaban
    /// abiertas o en curso, con su tiempo restante real.
    /// </summary>
    private async Task RehidratarCatasVivasAsync(CancellationToken ct)
    {
        try
        {
            using var ambito = _ambitos.CreateScope();
            var fabrica = ambito.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            var motor = ambito.ServiceProvider.GetRequiredService<ServicioVotacion>();

            await using var db = await fabrica.CreateDbContextAsync(ct);

            var ids = await db.Catas
                .AsNoTracking()
                .Where(c => c.Estado == EstadoCata.Abierta || c.Estado == EstadoCata.EnCurso)
                .Select(c => c.Id)
                .ToListAsync(ct);

            foreach (var id in ids)
            {
                await motor.RehidratarAsync(db, id, ct);
            }

            if (ids.Count > 0)
            {
                _log.LogInformation("Reloj de catas: {Total} cata(s) viva(s) recuperada(s) al arrancar.", ids.Count);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "No se pudieron recuperar las catas vivas al arrancar.");
        }
    }

    private async Task LatirAsync(CancellationToken ct)
    {
        var ahora = DateTime.UtcNow;

        foreach (var viva in _estado.Todas())
        {
            if (viva.Fase == FaseVotacion.Preparacion)
            {
                await LatirPreparacionAsync(viva, ahora, ct);
                continue;
            }

            if (viva.Fase == FaseVotacion.Abierta)
            {
                await LatirVotacionAsync(viva, ahora, ct);
                continue;
            }

            if (viva.Fase == FaseVotacion.Pausada)
            {
                // En pausa el reloj no avanza, pero seguimos latiendo para que
                // cualquier cliente que se reconecte vea el tiempo congelado.
                await _notificador.EnviarTickAsync(viva.CataId, viva.AEstado(ahora), ct);
            }
        }
    }

    /// <summary>Cuenta atrás 5 → 0. Al llegar a cero se abre la votación.</summary>
    private async Task LatirPreparacionAsync(CataViva viva, DateTime ahora, CancellationToken ct)
    {
        if (viva.AperturaPrevistaUtc is not DateTime apertura || apertura > ahora)
        {
            await _notificador.EnviarTickAsync(viva.CataId, viva.AEstado(ahora), ct);
            return;
        }

        var abierta = _estado.Mutar(viva.CataId, v => v with { Fase = FaseVotacion.Abierta });
        if (abierta is null)
        {
            return;
        }

        _log.LogInformation("Cata {CataId}: se abre la votación del vino nº {Orden}.", viva.CataId, viva.NumeroVinoActual);

        await _notificador.EnviarEstadoAsync(
            viva.CataId, EventosCata.VotacionAbierta, abierta.AEstado(DateTime.UtcNow), ct);
    }

    /// <summary>Tick de la votación: alerta roja y cierre por tiempo.</summary>
    private async Task LatirVotacionAsync(CataViva viva, DateTime ahora, CancellationToken ct)
    {
        var estado = viva.AEstado(ahora);

        if (viva.FinPrevistoUtc is DateTime fin && fin <= ahora)
        {
            await CerrarPorTiempoAsync(viva.CataId, ct);
            return;
        }

        if (estado.EnAlertaRoja && !viva.AlertaEnviada)
        {
            var marcada = _estado.Mutar(viva.CataId, v => v with { AlertaEnviada = true });
            await _notificador.EnviarEstadoAsync(
                viva.CataId,
                EventosCata.AlertaRoja,
                (marcada ?? viva).AEstado(ahora),
                ct);
            return;
        }

        await _notificador.EnviarTickAsync(viva.CataId, estado, ct);
    }

    private async Task CerrarPorTiempoAsync(int cataId, CancellationToken ct)
    {
        using var ambito = _ambitos.CreateScope();
        var fabrica = ambito.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var motor = ambito.ServiceProvider.GetRequiredService<ServicioVotacion>();

        using var _ = await _estado.BloquearAsync(ct);

        // Otro hilo (el administrador cerrando a mano) puede habernos
        // adelantado mientras esperábamos el cerrojo.
        if (_estado.Obtener(cataId) is not { Fase: FaseVotacion.Abierta })
        {
            return;
        }

        await using var db = await fabrica.CreateDbContextAsync(ct);
        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            _estado.Quitar(cataId);
            return;
        }

        await motor.CerrarVotacionInternaAsync(db, cata, ct);
        _log.LogInformation("Cata {CataId}: votación cerrada por agotarse el tiempo.", cataId);
    }
}
