using MisCatas.Domain.Enums;

namespace MisCatas.Application.Catas;

/// <summary>Datos de una cata para listados.</summary>
/// <param name="Id">Clave primaria.</param>
/// <param name="Nombre">Nombre del evento.</param>
/// <param name="Lugar">Dónde se celebra.</param>
/// <param name="Fecha">Fecha prevista.</param>
/// <param name="Estado">Estado del ciclo de vida.</param>
/// <param name="NumeroParticipantes">Cuántos usuarios están invitados.</param>
/// <param name="NumeroVinos">Cuántos vinos se catan.</param>
/// <param name="TiempoVotoSegundos">Tiempo de voto específico, o nulo si usa el global.</param>
/// <param name="Comentarios">Notas libres.</param>
public record CataDto(
    int Id,
    string Nombre,
    string? Lugar,
    DateTime Fecha,
    EstadoCata Estado,
    int NumeroParticipantes,
    int NumeroVinos,
    int? TiempoVotoSegundos,
    string? Comentarios);

/// <summary>Datos editables de una cata, comunes al alta y a la modificación.</summary>
public class GuardarCataDto
{
    /// <summary>Identificador. Cero al crear.</summary>
    public int Id { get; set; }

    /// <summary>Nombre del evento. Obligatorio.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Dónde se celebra. Opcional.</summary>
    public string? Lugar { get; set; }

    /// <summary>Fecha prevista de celebración.</summary>
    public DateTime Fecha { get; set; } = DateTime.Today;

    /// <summary>Notas libres. Opcional.</summary>
    public string? Comentarios { get; set; }

    /// <summary>
    /// Segundos para votar cada vino en esta cata. Nulo para usar el valor global.
    /// </summary>
    public int? TiempoVotoSegundos { get; set; }
}

/// <summary>Un vino asignado a una cata, con su posición en el recorrido.</summary>
/// <param name="CataVinoId">Identificador de la asignación.</param>
/// <param name="VinoId">Identificador del vino.</param>
/// <param name="NombreVino">Nombre del vino.</param>
/// <param name="Anio">Añada del vino.</param>
/// <param name="Orden">Número de orden dentro de la cata.</param>
/// <param name="EstadoVotacion">Estado de la votación de este vino.</param>
public record CataVinoDto(
    int CataVinoId,
    int VinoId,
    string NombreVino,
    int? Anio,
    int Orden,
    EstadoVotacion EstadoVotacion);

/// <summary>Un usuario invitado a una cata.</summary>
/// <param name="CataUsuarioId">Identificador de la invitación.</param>
/// <param name="UsuarioId">Identificador del usuario.</param>
/// <param name="NombreCompleto">Nombre completo del usuario.</param>
/// <param name="Login">Login del usuario.</param>
/// <param name="Rol">Rol del usuario.</param>
/// <param name="Telefono">
/// Teléfono del usuario. Se trae aquí porque es el canal por el que se le
/// puede avisar de la cata (WhatsApp). Nunca se usa para autenticar.
/// </param>
public record CataParticipanteDto(
    int CataUsuarioId,
    int UsuarioId,
    string NombreCompleto,
    string Login,
    Rol Rol,
    string Telefono);

/// <summary>Detalle completo de una cata con sus vinos y participantes.</summary>
/// <param name="Cata">Datos generales.</param>
/// <param name="Vinos">Vinos asignados, ordenados.</param>
/// <param name="Participantes">Usuarios invitados.</param>
public record CataDetalleDto(
    CataDto Cata,
    IReadOnlyList<CataVinoDto> Vinos,
    IReadOnlyList<CataParticipanteDto> Participantes);
