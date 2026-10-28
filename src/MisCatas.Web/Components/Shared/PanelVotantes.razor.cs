using Microsoft.AspNetCore.Components;
using MisCatas.Application.Participacion;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Mural con los invitados de la cata y su estado de voto en el vino en
/// juego. Solo presentación: los datos llegan por parámetro desde el progreso
/// que calcula el servidor.
/// </summary>
public partial class PanelVotantes
{
    /// <summary>Invitados con su estado de voto, ya ordenados por quien antes votó.</summary>
    [Parameter]
    public IReadOnlyList<ParticipanteVotoDto> Participantes { get; set; } = [];

    /// <summary>Rótulo de cabecera. Si va vacío no se pinta.</summary>
    [Parameter]
    public string? Titulo { get; set; } = "Quién ha votado";

    /// <summary>Tamaño según el dispositivo de destino.</summary>
    [Parameter]
    public TamanoCuentaAtras Tamano { get; set; } = TamanoCuentaAtras.Movil;

    /// <summary>Clases CSS extra.</summary>
    [Parameter]
    public string? Clase { get; set; }

    private bool EsEscaparate => Tamano == TamanoCuentaAtras.Escaparate;

    /// <summary>
    /// El mural tiene que caber SIEMPRE: en el escaparate no hay scroll y la
    /// pantalla es la que es. En vez de un tamaño fijo publicamos cuántos
    /// invitados hay y deja que el CSS calcule la letra; al encogerse la letra
    /// caben más columnas y bajan las filas, así que el bloque se autorregula
    /// igual con 4 personas que con 30.
    /// </summary>
    private string EstiloMural => $"--mural-personas: {Math.Max(Participantes.Count, 1)};";

    private static string ClaseFicha(ParticipanteVotoDto persona) => persona.HaVotado
        ? "mural-ficha mural-ficha--votado"
        : "mural-ficha";
}
