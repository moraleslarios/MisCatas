using MisCatas.Application.Catas;
using MisCatas.Application.Comun;
using MisCatas.Domain.Enums;

namespace MisCatas.Application.Avisos;

/// <summary>
/// Prepara avisos de WhatsApp para los participantes de una cata.
///
/// Importante: este servicio NO envía mensajes. Devuelve el texto y el enlace
/// oficial de WhatsApp; el envío lo hace siempre una persona, pulsando el
/// enlace y dando a «enviar» en su propio WhatsApp.
/// </summary>
public interface IServicioAvisosWhatsApp
{
    /// <summary>
    /// Hueco que la plantilla deja para el usuario de cada persona.
    /// </summary>
    const string HuecoUsuario = "{usuario}";

    /// <summary>
    /// Decide qué dirección se reparte a los invitados, partiendo de aquella
    /// por la que está navegando el organizador ahora mismo.
    /// </summary>
    /// <param name="direccionNavegador">
    /// Dirección base del navegador, tal como la da
    /// <c>NavigationManager.BaseUri</c>.
    /// </param>
    DireccionSitio ResolverDireccion(string? direccionNavegador);

    /// <summary>Aviso que corresponde por defecto al estado de la cata.</summary>
    TipoAvisoCata TipoSegunEstado(EstadoCata estado);

    /// <summary>
    /// Arma el texto del aviso con un hueco <c>{usuario}</c> donde después irá
    /// el usuario de cada persona. El organizador puede retocar este texto
    /// antes de repartirlo.
    /// </summary>
    /// <param name="detalle">Ficha completa de la cata.</param>
    /// <param name="tipo">Convocatoria o resultados.</param>
    /// <param name="raiz">
    /// Dirección de la aplicación que se mete en el mensaje, sin barra final.
    /// Si es <c>null</c>, el mensaje sale sin enlace.
    /// </param>
    string Plantilla(CataDetalleDto detalle, TipoAvisoCata tipo, string? raiz);

    /// <summary>
    /// Dirección exacta a la que lleva el aviso, o <c>null</c> si no hay
    /// ninguna raíz de la que tirar. Es también lo que se dibuja en el QR.
    /// </summary>
    string? Destino(int cataId, TipoAvisoCata tipo, string? raiz);

    /// <summary>
    /// Arma el texto del aviso igual que
    /// <see cref="Plantilla(CataDetalleDto, TipoAvisoCata, string?)"/>, pero
    /// añadiendo la segunda dirección (la que solo vale en el ordenador donde
    /// corre la aplicación) como alternativa cómoda.
    /// </summary>
    /// <param name="sitio">Las dos direcciones ya resueltas.</param>
    string Plantilla(CataDetalleDto detalle, TipoAvisoCata tipo, DireccionSitio sitio);

    /// <summary>
    /// Sustituye el hueco <c>{usuario}</c> de la plantilla por el usuario del
    /// participante indicado.
    /// </summary>
    string Personalizar(string plantilla, CataParticipanteDto participante);

    /// <summary>
    /// Enlace «wa.me» que abre WhatsApp con el destinatario y el texto ya
    /// puestos, o <c>null</c> si el teléfono no sirve.
    /// </summary>
    string? Enlace(string? telefono, string mensaje);

    /// <summary>
    /// Convierte un teléfono tal como lo escribió el administrador en el
    /// número internacional que exige WhatsApp, o <c>null</c> si no es válido.
    /// </summary>
    string? NormalizarTelefono(string? telefono);
}
