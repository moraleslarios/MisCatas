using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using MisCatas.Application.Catas;
using MisCatas.Application.Comun;
using MisCatas.Domain.Enums;

namespace MisCatas.Application.Avisos;

/* ---------------------------------------------------------------------------
   Avisos por WhatsApp.

   Cómo funciona, para que no haya sorpresas: la aplicación NO envía nada por
   su cuenta. No hay cuenta de empresa, ni API de pago, ni mensajes
   automáticos. Lo único que se hace aquí es PREPARAR el mensaje y el enlace
   oficial de WhatsApp («wa.me»); al pulsarlo se abre WhatsApp en el móvil o en
   el ordenador del organizador con el texto ya escrito y el destinatario ya
   elegido, y es él quien da a «enviar».

   Por qué así: es gratis, no necesita permisos de Meta, funciona desde el
   primer minuto y no guarda ni manda nada a terceros sin que nadie lo vea.
   --------------------------------------------------------------------------- */

/// <summary>
/// Prepara mensajes y enlaces de WhatsApp para avisar a los participantes de
/// una cata. No envía nada: solo deja el mensaje listo para enviar a mano.
/// </summary>
public class ServicioAvisosWhatsApp : IServicioAvisosWhatsApp
{
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Atajo al hueco que declara el contrato.</summary>
    private const string HuecoUsuario = IServicioAvisosWhatsApp.HuecoUsuario;

    private readonly IOptions<OpcionesSitio> _sitio;
    private readonly IServicioDireccionSitio _direccion;

    /// <summary>Crea el servicio con los datos del sitio.</summary>
    public ServicioAvisosWhatsApp(
        IOptions<OpcionesSitio> sitio,
        IServicioDireccionSitio direccion)
    {
        ArgumentNullException.ThrowIfNull(sitio);
        ArgumentNullException.ThrowIfNull(direccion);

        _sitio = sitio;
        _direccion = direccion;
    }

    /// <inheritdoc />
    public DireccionSitio ResolverDireccion(string? direccionNavegador) =>
        _direccion.Resolver(direccionNavegador);

    /// <inheritdoc />
    public TipoAvisoCata TipoSegunEstado(EstadoCata estado) =>
        estado == EstadoCata.Finalizada
            ? TipoAvisoCata.Resultados
            : TipoAvisoCata.Invitacion;

    /// <inheritdoc />
    public string? Destino(int cataId, TipoAvisoCata tipo, string? raiz)
    {
        if (string.IsNullOrWhiteSpace(raiz))
        {
            return null;
        }

        var limpia = raiz.TrimEnd('/');

        return tipo == TipoAvisoCata.Resultados
            ? $"{limpia}/catas/{cataId}/resultado"
            : limpia;
    }

    /// <inheritdoc />
    public string Plantilla(CataDetalleDto detalle, TipoAvisoCata tipo, string? raiz)
    {
        ArgumentNullException.ThrowIfNull(detalle);

        var cata = detalle.Cata;
        var texto = new StringBuilder();

        texto.Append("🍷 ").AppendLine(cata.Nombre);
        texto.AppendLine();

        if (tipo == TipoAvisoCata.Resultados)
        {
            texto.AppendLine("Ya están los resultados de la cata.");
        }
        else
        {
            texto.Append("📅 ").AppendLine(FechaLarga(cata.Fecha));

            if (!string.IsNullOrWhiteSpace(cata.Lugar))
            {
                texto.Append("📍 ").AppendLine(cata.Lugar);
            }

            if (detalle.Vinos.Count > 0)
            {
                texto
                    .Append("🍾 ")
                    .Append(detalle.Vinos.Count)
                    .AppendLine(detalle.Vinos.Count == 1
                        ? " vino a ciegas"
                        : " vinos a ciegas");
            }

            if (cata.TiempoVotoSegundos is int segundos and > 0)
            {
                texto
                    .Append("⏱ ")
                    .Append(segundos)
                    .AppendLine(" segundos para votar cada vino");
            }

            if (!string.IsNullOrWhiteSpace(cata.Comentarios))
            {
                texto.AppendLine();
                texto.AppendLine(cata.Comentarios!.Trim());
            }
        }

        texto.AppendLine();

        // El enlace es lo único que puede faltar: si no hay forma de saber por
        // qué dirección entran los invitados, el aviso se manda igual pero sin
        // enlace, porque un «localhost» en el móvil de otra persona no lleva a
        // ninguna parte.
        if (Destino(cata.Id, tipo, raiz) is { } destino)
        {
            texto.AppendLine(tipo == TipoAvisoCata.Resultados
                ? "Míralos aquí:"
                : "Entra desde el móvil:");
            texto.AppendLine(destino);
            texto.AppendLine();
        }

        // El hueco se sustituye luego por el login de cada persona. Así el
        // organizador puede retocar el texto una sola vez y que siga saliendo
        // personalizado para todos.
        texto.Append("👤 Tu usuario: ").AppendLine(HuecoUsuario);
        texto.AppendLine("🔑 La contraseña es tu número de teléfono (la primera vez te pedirá cambiarla).");

        texto.AppendLine();
        texto.AppendLine(tipo == TipoAvisoCata.Resultados
            ? "¡Gracias por catar!"
            : "¡Nos vemos en la mesa!");

        return texto.ToString().TrimEnd();
    }

    /// <inheritdoc />
    public string Personalizar(string plantilla, CataParticipanteDto participante)
    {
        ArgumentNullException.ThrowIfNull(plantilla);
        ArgumentNullException.ThrowIfNull(participante);

        return plantilla.Replace(
            HuecoUsuario,
            participante.Login,
            StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public string? Enlace(string? telefono, string mensaje)
    {
        ArgumentNullException.ThrowIfNull(mensaje);

        var numero = NormalizarTelefono(telefono);

        if (numero is null)
        {
            return null;
        }

        // Uri.EscapeDataString deja los saltos de línea y los emojis bien
        // codificados; WhatsApp los recompone tal cual en el cuadro de texto.
        return $"https://wa.me/{numero}?text={Uri.EscapeDataString(mensaje)}";
    }

    /// <inheritdoc />
    public string? NormalizarTelefono(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
        {
            return null;
        }

        // Se queda solo con las cifras: así da igual que esté escrito como
        // «+34 686 78 16 31», «0034-686781631» o «686781631».
        var cifras = new string(telefono.Where(char.IsDigit).ToArray());

        // Prefijo internacional escrito a la europea («00» en vez de «+»).
        if (cifras.StartsWith("00", StringComparison.Ordinal))
        {
            cifras = cifras[2..];
        }

        if (cifras.Length < 9)
        {
            return null;
        }

        var prefijo = _sitio.Value.PrefijoTelefonicoLimpio;

        // Número nacional de 9 cifras: le falta el prefijo del país, que la
        // aplicación sí conoce por configuración.
        if (cifras.Length == 9 && prefijo is not null)
        {
            cifras = prefijo + cifras;
        }

        return cifras.Length is >= 9 and <= 15 ? cifras : null;
    }

    /// <summary>Fecha larga en castellano, con la primera letra en mayúscula.</summary>
    private static string FechaLarga(DateTime fecha)
    {
        var texto = fecha.ToString("dddd d 'de' MMMM 'de' yyyy", Espanol);

        return char.ToUpper(texto[0], Espanol) + texto[1..];
    }
}
