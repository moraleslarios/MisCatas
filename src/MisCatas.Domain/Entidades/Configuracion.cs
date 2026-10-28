using MisCatas.Domain.Enums;

namespace MisCatas.Domain.Entidades;

/// <summary>
/// Parámetros globales de la aplicación. Tabla de una sola fila (Id = 1),
/// editable por un administrador.
/// </summary>
public class Configuracion
{
    /// <summary>Identificador de la fila única de configuración.</summary>
    public const int IdUnico = 1;

    /// <summary>Clave primaria. Siempre vale <see cref="IdUnico"/>.</summary>
    public int Id { get; set; } = IdUnico;

    /// <summary>
    /// Segundos de que dispone cada participante para votar un vino.
    /// Una cata concreta puede sobrescribirlo con <c>Cata.TiempoVotoSegundos</c>.
    /// </summary>
    public int TiempoVotoSegundos { get; set; } = 90;

    /// <summary>
    /// Porcentaje de tiempo restante por debajo del cual el cronómetro
    /// se muestra en rojo como alerta.
    /// </summary>
    public int PorcentajeAlertaRoja { get; set; } = 25;

    /// <summary>Duración en segundos de la animación de revelación de resultados.</summary>
    public int SegundosRevelacionFinal { get; set; } = 20;

    /// <summary>
    /// Si es <c>true</c>, el administrador puede pasar al siguiente vino
    /// aunque falten votos por emitir.
    /// </summary>
    public bool PermitirAvanzarSinTodosLosVotos { get; set; } = true;

    /// <summary>Qué hacer cuando un participante no vota a tiempo.</summary>
    public PoliticaNoVoto PoliticaNoVoto { get; set; } = PoliticaNoVoto.NoCuenta;

    /// <summary>
    /// Nota que se asigna a un no-voto cuando la política es
    /// <see cref="PoliticaNoVoto.NotaFija"/>. Nula con la política por defecto.
    /// </summary>
    public decimal? NotaPorDefectoNoVoto { get; set; }

    /// <summary>Comentario que se escribe en los votos generados automáticamente.</summary>
    public string ComentarioAutomaticoNoVoto { get; set; } = "No votado";

    /// <summary>Incremento mínimo permitido al puntuar (p. ej. 0,5).</summary>
    public decimal PasoDecimalNota { get; set; } = 0.5m;
}
