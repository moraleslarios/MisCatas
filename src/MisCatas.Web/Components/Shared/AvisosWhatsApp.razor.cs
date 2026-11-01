using System.Globalization;
using Microsoft.AspNetCore.Components;
using MisCatas.Application.Avisos;
using MisCatas.Application.Catas;
using MisCatas.Application.Comun;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Panel para avisar a los participantes de una cata por WhatsApp.
///
/// No envía mensajes: prepara el texto y, por cada persona, un enlace que abre
/// WhatsApp con la conversación y el mensaje ya puestos. El envío lo hace
/// siempre el organizador a mano.
///
/// No consulta nada a la base de datos: recibe el detalle ya cargado por la
/// pantalla que lo muestra.
/// </summary>
public partial class AvisosWhatsApp
{
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Tipos de aviso que se ofrecen, en el orden en que se pintan.</summary>
    private static readonly TipoAvisoCata[] Tipos =
    [
        TipoAvisoCata.Invitacion,
        TipoAvisoCata.Resultados,
    ];

    private readonly string _id = Guid.NewGuid().ToString("N")[..8];

    /// <summary>Cata a la que se refiere el aviso, ya cargada.</summary>
    [Parameter]
    public CataDetalleDto? Detalle { get; set; }

    [Inject]
    private IServicioAvisosWhatsApp Avisos { get; set; } = default!;

    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    /// <summary>Aviso elegido ahora mismo.</summary>
    private TipoAvisoCata Tipo { get; set; } = TipoAvisoCata.Invitacion;

    /// <summary>
    /// Dirección que se va a repartir a los invitados, deducida de aquella por
    /// la que está navegando el organizador ahora mismo.
    /// </summary>
    private DireccionSitio Sitio { get; set; }

    /// <summary>
    /// Dirección exacta a la que lleva el aviso. Es lo que se dibuja en el QR.
    /// </summary>
    private string? Destino => Detalle is { } detalle
        ? Avisos.Destino(detalle.Cata.Id, Tipo, Sitio.Raiz)
        : null;

    /// <summary>
    /// Segunda dirección a la que lleva el aviso: la del nombre que solo
    /// funciona en este ordenador. Se enseña como texto, nunca en el QR.
    /// </summary>
    private string? DestinoEnEsteEquipo => Detalle is { } detalle && Sitio.TieneAlternativaLocal
        ? Avisos.Destino(detalle.Cata.Id, Tipo, Sitio.SoloEnEsteEquipo)
        : null;

    /// <summary>
    /// Texto que se va a enviar. Arranca con la propuesta del servicio y el
    /// organizador puede retocarlo antes de repartirlo.
    /// </summary>
    private string Texto { get; set; } = string.Empty;

    /// <summary>
    /// Para saber si hace falta rehacer la propuesta: si cambia la cata, el
    /// texto que había escrito ya no vale.
    /// </summary>
    private int _idCataPintada;

    protected override void OnParametersSet()
    {
        if (Detalle is not { } detalle)
        {
            return;
        }

        // La dirección se vuelve a resolver siempre: es cosa de la navegación,
        // no de la cata.
        Sitio = Avisos.ResolverDireccion(Navegacion.BaseUri);

        if (_idCataPintada == detalle.Cata.Id)
        {
            return;
        }

        _idCataPintada = detalle.Cata.Id;

        // El aviso que toca se adivina por el estado: una cata finalizada pide
        // resultados, cualquier otra pide convocatoria.
        Tipo = Avisos.TipoSegunEstado(detalle.Cata.Estado);
        RestaurarTexto();
    }

    /// <summary>Vuelve a poner el texto que propone la aplicación.</summary>
    private void RestaurarTexto()
    {
        if (Detalle is { } detalle)
        {
            Texto = Avisos.Plantilla(detalle, Tipo, Sitio);
        }
    }

    private void CambiarTipo(TipoAvisoCata tipo)
    {
        if (Tipo == tipo)
        {
            return;
        }

        Tipo = tipo;

        // Al cambiar de aviso el texto anterior ya no pega, así que se rehace.
        RestaurarTexto();
    }

    /// <summary>Enlace de WhatsApp de un participante, o <c>null</c> si no se puede.</summary>
    private string? EnlaceDe(CataParticipanteDto participante)
    {
        var mensaje = Avisos.Personalizar(Texto, participante);

        return Avisos.Enlace(participante.Telefono, mensaje);
    }

    private string ClaseTipo(TipoAvisoCata tipo) => Tipo == tipo
        ? "avisos-tipo avisos-tipo-activo"
        : "avisos-tipo";

    private static string NombreTipo(TipoAvisoCata tipo) => tipo switch
    {
        TipoAvisoCata.Resultados => "Ya hay resultados",
        _ => "Convocatoria",
    };

    /// <summary>Iniciales del participante, para su ficha redonda.</summary>
    private static string Iniciales(string nombreCompleto)
    {
        var trozos = nombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (trozos.Length == 0)
        {
            return "·";
        }

        return trozos.Length == 1
            ? trozos[0][..1].ToUpper(Espanol)
            : string.Concat(trozos[0][..1], trozos[1][..1]).ToUpper(Espanol);
    }
}
