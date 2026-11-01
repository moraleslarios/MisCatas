using MisCatas.Domain.Enums;

namespace MisCatas.Application.Participacion;

/* ---------------------------------------------------------------------------
   DTOs de participación: lo que ve un usuario (o el escaparate) de una cata.

   REGLA DE ORO DE LA CATA CIEGA
   Mientras la cata no esté FINALIZADA, de un vino solo se puede conocer su
   NÚMERO DE ORDEN. Ni nombre, ni año, ni clase, ni bodega, ni dueño, ni
   precio, ni foto, ni etiqueta.

   Por eso la ocultación se hace AQUÍ, con DTOs distintos proyectados en el
   servidor, y nunca escondiendo campos con CSS ni mandando la entidad
   completa al cliente:

     · VinoCiegoDto      cata en marcha  -> solo el número de orden
     · VinoReveladoDto   cata finalizada -> ya se puede contar todo

   Los resúmenes en vivo (usuario y escaparate) solo manejan métricas
   AGREGADAS (ProgresoCataDto): cuántos han votado, cuántos faltan y el
   tiempo medio. Nunca notas ni datos del vino.
   --------------------------------------------------------------------------- */

/// <summary>
/// Un vino de una cata que todavía NO ha terminado: a ojos del usuario es
/// únicamente «Vino nº N». Incluye el voto propio porque cada cual sí puede
/// ver y rectificar lo que él mismo ha puntuado.
/// </summary>
public sealed record VinoCiegoDto(
    int CataVinoId,
    int NumeroOrden,
    EstadoVotacion EstadoVotacion,
    bool HeVotado,
    decimal? MiNota,
    string? MiComentario)
{
    /// <summary>Etiqueta visible: «Vino nº 3».</summary>
    public string Titulo => $"Vino nº {NumeroOrden}";
}

/// <summary>
/// Un vino de una cata ya finalizada: aquí sí se desvela todo, junto con el
/// resultado de la votación y la posición en el ranking.
/// </summary>
public sealed record VinoReveladoDto(
    int Posicion,
    int NumeroOrden,
    int VinoId,
    string Nombre,
    int? Anio,
    string? Clase,
    string? Bodega,
    string? DenominacionOrigen,
    string Dueno,
    decimal? PrecioEstimado,
    string? Comentarios,
    bool TieneFoto,
    bool TieneEtiqueta,
    decimal? NotaMedia,
    int VotosContados,
    decimal? MiNota,
    string? MiComentario,
    IReadOnlyList<VotoReveladoDto> Votos)
{
    public string NombreConAnio => Anio is null ? Nombre : $"{Nombre} ({Anio})";

    /// <summary>Medalla para los tres primeros; null para el resto.</summary>
    public string? Medalla => Posicion switch
    {
        1 => "Oro",
        2 => "Plata",
        3 => "Bronce",
        _ => null
    };

    /// <summary>Nota más alta recibida.</summary>
    public decimal? MejorNota => Votos.Where(v => v.Nota is not null).Max(v => v.Nota);

    /// <summary>Nota más baja recibida.</summary>
    public decimal? PeorNota => Votos.Where(v => v.Nota is not null).Min(v => v.Nota);

    /// <summary>Cuántos participantes dejaron comentario.</summary>
    public int Comentaristas => Votos.Count(v => v.TieneComentario);
}

/// <summary>Estado de una cata tal y como se lista en «Mis catas».</summary>
public sealed record CataUsuarioDto(
    int Id,
    string Nombre,
    string? Lugar,
    DateTime Fecha,
    EstadoCata Estado,
    int NumeroVinos,
    int NumeroParticipantes,
    int MisVotos)
{
    public bool EstaFinalizada => Estado == EstadoCata.Finalizada;
    public bool EstaEnCurso => Estado == EstadoCata.EnCurso;
}

/// <summary>
/// Las catas del usuario repartidas en los tres bloques de la pantalla
/// principal del rol Usuario.
/// </summary>
public sealed record MisCatasDto(
    IReadOnlyList<CataUsuarioDto> EnCurso,
    IReadOnlyList<CataUsuarioDto> Proximas,
    IReadOnlyList<CataUsuarioDto> Historico)
{
    public static MisCatasDto Vacio => new([], [], []);

    public bool NoHayNada => EnCurso.Count == 0 && Proximas.Count == 0 && Historico.Count == 0;
}

/// <summary>
/// Métricas AGREGADAS del avance de una cata. No contiene ni una sola nota
/// ni ningún dato identificativo de vino: es lo único que se puede mostrar
/// mientras la cata está viva.
/// </summary>
public sealed record ProgresoCataDto(
    int TotalParticipantes,
    int HanVotado,
    int Faltan,
    double? TiempoMedioSegundos,
    int TotalVinos,
    int VinosCerrados,
    int? NumeroVinoActual,
    EstadoVotacion? EstadoVinoActual,
    IReadOnlyList<ParticipanteVotoDto>? Participantes = null)
{
    /// <summary>
    /// Participantes con su estado de voto, siempre una lista utilizable.
    /// Es opcional en el constructor para que las pantallas que solo quieren
    /// los recuentos no tengan que fabricarla.
    /// </summary>
    public IReadOnlyList<ParticipanteVotoDto> Votantes => Participantes ?? [];

    /// <summary>Porcentaje de votos recibidos del vino en curso, 0-100.</summary>
    public int PorcentajeVotado => TotalParticipantes == 0
        ? 0
        : (int)Math.Round(HanVotado * 100d / TotalParticipantes, MidpointRounding.AwayFromZero);

    /// <summary>Porcentaje de vinos ya cerrados, 0-100.</summary>
    public int PorcentajeCata => TotalVinos == 0
        ? 0
        : (int)Math.Round(VinosCerrados * 100d / TotalVinos, MidpointRounding.AwayFromZero);

    public string TiempoMedioTexto => TiempoMedioSegundos is null or 0
        ? "—"
        : $"{TiempoMedioSegundos.Value:0.#} s";
}

/// <summary>
/// Vista ciega completa de una cata para un participante: la cabecera, el
/// progreso y la lista de vinos reducida a números de orden.
/// </summary>
public sealed record CataCiegaDto(
    int CataId,
    string Nombre,
    string? Lugar,
    DateTime Fecha,
    EstadoCata Estado,
    int TiempoVotoSegundos,
    decimal PasoDecimalNota,
    int? NumeroVinoActual,
    EstadoVotacion? EstadoVinoActual,
    IReadOnlyList<VinoCiegoDto> Vinos,
    ProgresoCataDto Progreso)
{
    public VinoCiegoDto? VinoEnVotacion =>
        Vinos.FirstOrDefault(v => v.NumeroOrden == NumeroVinoActual);

    public int MisVotos => Vinos.Count(v => v.HeVotado);
}

/// <summary>Resultado final de una cata finalizada: todo al descubierto.</summary>
public sealed record ResultadoCataDto(
    int CataId,
    string Nombre,
    string? Lugar,
    DateTime Fecha,
    string? Comentarios,
    int NumeroParticipantes,
    IReadOnlyList<VinoReveladoDto> Clasificacion)
{
    public VinoReveladoDto? Ganador => Clasificacion.Count == 0 ? null : Clasificacion[0];
}

/// <summary>
/// Lo que toca pintar ahora mismo en la pantalla de escaparate.
/// </summary>
public enum PantallaEscaparate
{
    /// <summary>No hay nada que mostrar: pantalla de bienvenida.</summary>
    Espera = 0,

    /// <summary>Hay una votación abierta: cuenta atrás y número de vino.</summary>
    Votacion = 1,

    /// <summary>Votación cerrada pero cata viva: resumen en vivo.</summary>
    Resumen = 2,

    /// <summary>Cata finalizada: revelación del ranking.</summary>
    Revelacion = 3
}

/// <summary>
/// Estado agregado de la cata que debe proyectar el escaparate. Igual que el
/// resto de vistas en vivo, no lleva notas ni datos de vino salvo cuando la
/// cata ya está finalizada (y entonces vienen en <see cref="Resultado"/>).
/// </summary>
public sealed record CataEscaparateDto(
    PantallaEscaparate Pantalla,
    int? CataId,
    string? Nombre,
    string? Lugar,
    DateTime? Fecha,
    int? NumeroVinoActual,
    int TotalVinos,
    ProgresoCataDto? Progreso,
    ResultadoCataDto? Resultado,
    CataUsuarioDto? ProximaCata,
    int TiempoVotoSegundos = 90,
    int PorcentajeAlertaRoja = 25)
{
    public static CataEscaparateDto EnEspera(CataUsuarioDto? proxima = null) =>
        new(PantallaEscaparate.Espera, proxima?.Id, null, null, null, null, 0, null, null, proxima);

    /// <summary>
    /// True cuando, estando en pantalla de espera, la cata anunciada ya está
    /// activa (abierta o en curso). Sirve para avisar en la televisión de que
    /// la cata está en marcha y de cómo entrar a votar.
    /// </summary>
    public bool CataAnunciadaActiva =>
        ProximaCata is not null && ProximaCata.Estado is EstadoCata.Abierta or EstadoCata.EnCurso;

    /// <summary>
    /// Rótulo con el que se presenta la cata anunciada en la televisión.
    ///
    /// Son cuatro situaciones distintas y la sala las vive de forma distinta:
    /// si ya se está votando, es la cata en marcha; si las puertas están
    /// abiertas pero aún no se vota, hay que mover a la gente a coger el móvil
    /// sin dar a entender que llegan tarde; si la cata es de hoy pero todavía
    /// está en el cajón, es la antesala; y si es de otro día, es cartelera.
    /// </summary>
    public string RotuloCataAnunciada
    {
        get
        {
            if (ProximaCata is null)
            {
                return "Próxima cata";
            }

            if (ProximaCata.Estado == EstadoCata.EnCurso)
            {
                return "Cata en marcha";
            }

            if (ProximaCata.Estado == EstadoCata.Abierta)
            {
                return "Pendiente de empezar · ya puedes entrar";
            }

            return ProximaCata.Fecha.Date == DateTime.Today
                ? "Hoy · pendiente de realizarse"
                : "Próxima cata";
        }
    }

    /// <summary>
    /// Frase que explica en qué punto está la cata anunciada cuando todavía no
    /// se puede entrar a votar. Es <c>null</c> cuando las puertas ya están
    /// abiertas, porque entonces la pantalla muestra el QR y la dirección.
    /// </summary>
    public string? EsperaCataAnunciada
    {
        get
        {
            if (ProximaCata is null || CataAnunciadaActiva)
            {
                return null;
            }

            return ProximaCata.Fecha.Date == DateTime.Today
                ? "Pendiente de realizarse: en cuanto se abra, podrás entrar a votar desde el móvil."
                : "Pendiente de realizarse. Avisaremos en esta pantalla cuando se abra.";
        }
    }
}

/// <summary>
/// Voto individual de un participante sobre un vino de una cata ya finalizada.
/// Solo se proyecta cuando la cata está cerrada: antes no existe.
/// </summary>
public sealed record VotoReveladoDto(
    int UsuarioId,
    string NombreUsuario,
    decimal? Nota,
    string? Comentario,
    int SegundosEnVotar,
    bool FueRectificado,
    bool EsAutomatico)
{
    /// <summary>Nota en texto es-ES, o un guion si no votó.</summary>
    public string NotaTexto => Nota is decimal nota
        ? nota.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("es-ES"))
        : "—";

    /// <summary>Tiempo en formato corto para la tabla.</summary>
    public string TiempoTexto => EsAutomatico || SegundosEnVotar <= 0
        ? "—"
        : $"{SegundosEnVotar} s";

    /// <summary>True cuando el voto lo generó la política de no-voto.</summary>
    public bool NoVoto => EsAutomatico;

    public bool TieneComentario => !string.IsNullOrWhiteSpace(Comentario);
}

/// <summary>
/// Un participante de la cata tal y como se le puede mostrar a TODA la sala
/// mientras se vota: su nombre, si ya ha echado la papeleta y cuánto tardó.
///
/// Deliberadamente NO lleva la nota: la cata es a ciegas y saber quién ha
/// votado no es lo mismo que saber qué ha puntuado.
/// </summary>
/// <param name="UsuarioId">Identificador, útil como clave de la lista.</param>
/// <param name="Nombre">Nombre de pila, que es lo que se lee de lejos en la televisión.</param>
/// <param name="HaVotado">Ya ha votado el vino en juego.</param>
/// <param name="SegundosEnVotar">Lo que tardó, si votó.</param>
public sealed record ParticipanteVotoDto(
    int UsuarioId,
    string Nombre,
    bool HaVotado,
    int? SegundosEnVotar)
{
    /// <summary>Tiempo listo para rotular, o raya si todavía no ha votado.</summary>
    public string TiempoTexto => HaVotado && SegundosEnVotar is int s ? $"{s} s" : "—";
}
