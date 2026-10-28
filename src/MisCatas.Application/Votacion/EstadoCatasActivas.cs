using System.Collections.Concurrent;
using MisCatas.Domain.Enums;

namespace MisCatas.Application.Votacion;

/// <summary>
/// Foto inmutable del estado en vivo de una cata. Al ser inmutable se puede
/// leer desde cualquier hilo sin cerrojos: las mutaciones sustituyen la foto
/// completa de golpe.
/// </summary>
public sealed record CataViva(
    int CataId,
    EstadoCata EstadoCata,
    int TotalVinos,
    int SegundosTotales,
    int PorcentajeAlertaRoja,
    int? NumeroVinoActual,
    FaseVotacion Fase,
    DateTime? AperturaPrevistaUtc,
    DateTime? FinPrevistoUtc,
    bool AlertaEnviada,
    bool RevelacionActiva,
    int? AdminControlaId,
    int? SegundosPausaRestantes = null)
{
    /// <summary>Cata recién cargada, sin votación en marcha.</summary>
    public static CataViva EnReposo(
        int cataId,
        EstadoCata estadoCata,
        int totalVinos,
        int segundosTotales,
        int porcentajeAlertaRoja,
        bool revelacionActiva,
        int? adminControlaId) =>
        new(
            cataId,
            estadoCata,
            totalVinos,
            segundosTotales,
            porcentajeAlertaRoja,
            NumeroVinoActual: null,
            Fase: FaseVotacion.Inactiva,
            AperturaPrevistaUtc: null,
            FinPrevistoUtc: null,
            AlertaEnviada: false,
            revelacionActiva,
            adminControlaId);

    /// <summary>Proyecta la foto al DTO que viaja a los clientes.</summary>
    public EstadoVivoDto AEstado(DateTime ahoraUtc)
    {
        // En pausa el reloj NO corre: se envía el remanente congelado tal cual,
        // así todos los clientes pintan la misma cifra quieta.
        var restantes = Fase switch
        {
            FaseVotacion.Abierta when FinPrevistoUtc is DateTime fin
                => Math.Max(0d, (fin - ahoraUtc).TotalSeconds),
            FaseVotacion.Pausada => SegundosPausaRestantes ?? 0d,
            _ => 0d,
        };

        var preparacion = Fase == FaseVotacion.Preparacion && AperturaPrevistaUtc is DateTime apertura
            ? Math.Max(0, (int)Math.Ceiling((apertura - ahoraUtc).TotalSeconds))
            : 0;

        return new EstadoVivoDto(
            CataId,
            EstadoCata,
            Fase,
            NumeroVinoActual,
            TotalVinos,
            SegundosTotales,
            PorcentajeAlertaRoja,
            FinPrevistoUtc,
            restantes,
            preparacion,
            RevelacionActiva,
            AdminControlaId);
    }
}

/// <summary>
/// Registro en memoria de las catas vivas. Es el reloj autoritativo del
/// servidor: el tiempo restante que ven los móviles y las televisiones sale de
/// aquí, nunca de un temporizador del navegador.
///
/// Se respalda en la base de datos (<c>InicioVotacionUtc</c> y
/// <c>FinPrevistoUtc</c> de cada <c>CataVino</c>) para sobrevivir a
/// reconexiones y al reinicio del proceso.
/// </summary>
public sealed class EstadoCatasActivas
{
    /// <summary>Duración de la cuenta atrás de preparación: 5 → 4 → 3 → 2 → 1 → 0.</summary>
    public const int SegundosPreparacion = 5;

    private readonly ConcurrentDictionary<int, CataViva> _catas = new();
    private readonly SemaphoreSlim _cerrojo = new(1, 1);

    /// <summary>
    /// Cerrojo de escritura. Todas las acciones del administrador y los votos
    /// se serializan con él para que dos clics simultáneos no se solapen.
    /// </summary>
    public async Task<IDisposable> BloquearAsync(CancellationToken ct = default)
    {
        await _cerrojo.WaitAsync(ct);
        return new Liberador(_cerrojo);
    }

    /// <summary>Foto actual de una cata, si está cargada.</summary>
    public CataViva? Obtener(int cataId) =>
        _catas.TryGetValue(cataId, out var viva) ? viva : null;

    /// <summary>Guarda o sustituye la foto de una cata.</summary>
    public CataViva Guardar(CataViva viva)
    {
        ArgumentNullException.ThrowIfNull(viva);
        _catas[viva.CataId] = viva;
        return viva;
    }

    /// <summary>Muta la foto de una cata ya cargada.</summary>
    public CataViva? Mutar(int cataId, Func<CataViva, CataViva> cambio)
    {
        ArgumentNullException.ThrowIfNull(cambio);

        if (!_catas.TryGetValue(cataId, out var actual))
        {
            return null;
        }

        var nueva = cambio(actual);
        _catas[cataId] = nueva;
        return nueva;
    }

    /// <summary>Saca una cata del registro (al finalizarla, por ejemplo).</summary>
    public void Quitar(int cataId) => _catas.TryRemove(cataId, out _);

    /// <summary>Todas las catas cargadas, para que el temporizador las recorra.</summary>
    public IReadOnlyList<CataViva> Todas() => [.. _catas.Values];

    private sealed class Liberador(SemaphoreSlim cerrojo) : IDisposable
    {
        private bool _liberado;

        public void Dispose()
        {
            if (_liberado)
            {
                return;
            }

            _liberado = true;
            cerrojo.Release();
        }
    }
}
