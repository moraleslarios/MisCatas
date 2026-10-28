using MisCatas.Domain.Enums;

namespace MisCatas.Application.Votacion;

/// <summary>
/// Fase en la que se encuentra la votación del vino en juego.
///
/// El reloj es AUTORITATIVO EN EL SERVIDOR: esta fase y el instante de fin
/// los decide el servidor y los clientes solo los pintan. Nunca al contrario.
/// </summary>
public enum FaseVotacion
{
    /// <summary>No hay ningún vino en juego.</summary>
    Inactiva = 0,

    /// <summary>Cuenta atrás de preparación (5 → 0) antes de abrir el voto.</summary>
    Preparacion = 1,

    /// <summary>Votación abierta: se aceptan notas.</summary>
    Abierta = 2,

    /// <summary>Votación cerrada: ventana de rectificación de votos anteriores.</summary>
    Cerrada = 3,

    /// <summary>
    /// Votación en marcha pero CONGELADA por el administrador. El reloj no
    /// corre y no se aceptan votos nuevos; al reanudar se recupera el tiempo
    /// exacto que quedaba.
    /// </summary>
    Pausada = 4,
}

/// <summary>
/// Foto del estado en vivo de una cata. Es lo que se envía por SignalR y lo
/// que recupera un cliente que recarga o se reconecta a mitad de votación.
///
/// No contiene ningún dato del vino: solo su número de orden.
/// </summary>
/// <param name="CataId">Cata a la que se refiere el estado.</param>
/// <param name="EstadoCata">Estado de la máquina de estados de la cata.</param>
/// <param name="Fase">Fase de la votación del vino en juego.</param>
/// <param name="NumeroVinoActual">Número de orden del vino en juego, si hay alguno.</param>
/// <param name="TotalVinos">Vinos que tiene la cata.</param>
/// <param name="SegundosTotales">Duración configurada de la votación.</param>
/// <param name="PorcentajeAlertaRoja">Porcentaje restante a partir del cual se avisa.</param>
/// <param name="FinPrevistoUtc">Instante UTC en el que el servidor cerrará la votación.</param>
/// <param name="SegundosRestantes">Segundos que faltan según el reloj del servidor.</param>
/// <param name="SegundosPreparacion">Segundos que faltan de la cuenta atrás de preparación.</param>
/// <param name="RevelacionActiva">Si la revelación final está proyectándose.</param>
/// <param name="AdminControlaId">Administrador que gobierna la cata.</param>
public sealed record EstadoVivoDto(
    int CataId,
    EstadoCata EstadoCata,
    FaseVotacion Fase,
    int? NumeroVinoActual,
    int TotalVinos,
    int SegundosTotales,
    int PorcentajeAlertaRoja,
    DateTime? FinPrevistoUtc,
    double SegundosRestantes,
    int SegundosPreparacion,
    bool RevelacionActiva,
    int? AdminControlaId)
{
    /// <summary>Segundos restantes redondeados hacia arriba, para rotular.</summary>
    public int SegundosRestantesEnteros => (int)Math.Ceiling(Math.Max(0d, SegundosRestantes));

    /// <summary>Votación congelada por el administrador.</summary>
    public bool EstaPausada => Fase == FaseVotacion.Pausada;

    /// <summary>Umbral, en segundos, a partir del cual la cuenta atrás es crítica.</summary>
    public double UmbralAlertaSegundos => SegundosTotales * PorcentajeAlertaRoja / 100d;

    /// <summary>
    /// Zona de peligro: queda menos del porcentaje configurado. Los clientes
    /// deben acompañarlo de texto e icono, nunca solo de color.
    /// </summary>
    public bool EnAlertaRoja =>
        Fase == FaseVotacion.Abierta
        && SegundosTotales > 0
        && SegundosRestantes <= UmbralAlertaSegundos;

    /// <summary>Se aceptan votos del vino en juego.</summary>
    public bool AceptaVotos => Fase == FaseVotacion.Abierta && SegundosRestantes > 0d;

    /// <summary>Ventana de rectificación: votación cerrada y cata todavía viva.</summary>
    public bool PermiteRectificar =>
        EstadoCata == EstadoCata.EnCurso && Fase is FaseVotacion.Cerrada or FaseVotacion.Inactiva;

    /// <summary>Estado neutro de una cata que no tiene votación en marcha.</summary>
    public static EstadoVivoDto EnReposo(
        int cataId,
        EstadoCata estadoCata,
        int totalVinos,
        int segundosTotales,
        int porcentajeAlertaRoja,
        bool revelacionActiva = false,
        int? adminControlaId = null) =>
        new(
            cataId,
            estadoCata,
            FaseVotacion.Inactiva,
            null,
            totalVinos,
            segundosTotales,
            porcentajeAlertaRoja,
            null,
            0d,
            0,
            revelacionActiva,
            adminControlaId);
}

/// <summary>
/// Voto que un usuario tiene registrado en un vino, tal y como se le devuelve
/// a él mismo. Solo lleva el número de orden del vino.
/// </summary>
public sealed record MiVotoDto(
    int NumeroOrden,
    decimal? Nota,
    string? Comentario,
    int SegundosEnVotar,
    bool FueRectificado);

/// <summary>
/// Notas de un vino, sin identidad, para alimentar la calculadora de
/// resultados. Se usa igual desde la base de datos que desde las pruebas.
/// </summary>
/// <param name="NumeroOrden">Orden del vino dentro de la cata.</param>
/// <param name="VinoId">Identificador del vino.</param>
/// <param name="Notas">Notas válidas (ya excluidos los no-votos que no cuentan).</param>
/// <param name="SegundosEnVotar">Tiempos de los votos reales, para la media.</param>
public sealed record NotasDeVino(
    int NumeroOrden,
    int VinoId,
    IReadOnlyList<decimal> Notas,
    IReadOnlyList<int> SegundosEnVotar);

/// <summary>
/// Métricas calculadas de un vino. El desempate por desviación típica vive
/// aquí para poder probarlo sin tocar la base de datos.
/// </summary>
public sealed record MetricasVino(
    int Posicion,
    int NumeroOrden,
    int VinoId,
    decimal? Media,
    int VotosContados,
    decimal? MejorNota,
    decimal? PeorNota,
    decimal? DesviacionTipica,
    double? TiempoMedioSegundos)
{
    /// <summary>Media formateada en castellano, o raya si no hubo votos.</summary>
    public string MediaTexto => Media is null ? "—" : Media.Value.ToString("0.0");

    /// <summary>Consenso formateado, o raya si no procede.</summary>
    public string DesviacionTexto => DesviacionTipica is null ? "—" : DesviacionTipica.Value.ToString("0.00");

    /// <summary>Tiempo medio formateado.</summary>
    public string TiempoMedioTexto =>
        TiempoMedioSegundos is null or 0 ? "—" : $"{TiempoMedioSegundos.Value:0.#} s";
}

/// <summary>
/// Nombres de los eventos que el servidor envía a los clientes por SignalR.
/// Se centralizan aquí para que el emisor y los suscriptores no se
/// desincronicen por una cadena mal escrita.
/// </summary>
public static class EventosCata
{
    public const string CataAbierta = nameof(CataAbierta);
    public const string CuentaAtrasIniciada = nameof(CuentaAtrasIniciada);
    public const string VotacionAbierta = nameof(VotacionAbierta);
    public const string TickTiempoRestante = nameof(TickTiempoRestante);
    public const string AlertaRoja = nameof(AlertaRoja);
    public const string VotacionCerrada = nameof(VotacionCerrada);
    public const string VotacionPausada = nameof(VotacionPausada);
    public const string VotacionReanudada = nameof(VotacionReanudada);
    public const string ResumenActualizado = nameof(ResumenActualizado);
    public const string VotoReseteado = nameof(VotoReseteado);
    public const string CataFinalizada = nameof(CataFinalizada);
    public const string RevelacionIniciada = nameof(RevelacionIniciada);
    public const string RevelacionOcultada = nameof(RevelacionOcultada);
    public const string CataReabierta = nameof(CataReabierta);

    /// <summary>Nombre del grupo de SignalR de una cata.</summary>
    public static string Grupo(int cataId) => $"cata-{cataId}";

    /// <summary>Subgrupo por rol dentro de una cata.</summary>
    public static string Grupo(int cataId, Rol rol) => $"cata-{cataId}-{rol}";
}
