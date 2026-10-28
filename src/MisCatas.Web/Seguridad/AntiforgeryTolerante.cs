using Microsoft.AspNetCore.Antiforgery;

namespace MisCatas.Web.Seguridad;

/// <summary>
/// Red de seguridad para los formularios renderizados en servidor.
/// </summary>
/// <remarks>
/// Cuando un móvil deja la pantalla de acceso abierta mucho tiempo (o la
/// recupera de la caché del navegador al pulsar «atrás») y luego envía el
/// formulario, el token de antiforgery puede haber caducado o pertenecer a
/// un arranque anterior del servidor. Sin esto, el usuario ve el error crudo
/// «A valid antiforgery token was not provided with the request». Con esto se
/// le devuelve la misma página recién pintada y con token nuevo, así que
/// basta con volver a enviar el formulario.
/// </remarks>
public static class AntiforgeryTolerante
{
    /// <summary>
    /// Convierte el fallo de validación del token en una recarga de la misma
    /// página mediante GET.
    /// </summary>
    /// <remarks>
    /// Debe registrarse DESPUÉS de <c>UseAntiforgery</c>: ese middleware no
    /// lanza excepción, deja el resultado en
    /// <see cref="IAntiforgeryValidationFeature"/> y es Blazor quien más
    /// adelante responde 400. Aquí se intercepta antes de que eso ocurra.
    /// </remarks>
    public static IApplicationBuilder UsarAntiforgeryTolerante(
        this IApplicationBuilder aplicacion)
    {
        ArgumentNullException.ThrowIfNull(aplicacion);

        return aplicacion.Use(async (contexto, siguiente) =>
        {
            if (!DebeRepintar(contexto))
            {
                await siguiente(contexto);
                return;
            }

            // Se descarta el cuerpo enviado y se repinta la página: es la
            // única forma de entregar un token nuevo y coherente.
            var destino = contexto.Request.Path + contexto.Request.QueryString;
            contexto.Response.Redirect(destino);
        });
    }

    private static bool DebeRepintar(HttpContext contexto)
    {
        // Solo envíos de formulario.
        if (!HttpMethods.IsPost(contexto.Request.Method))
        {
            return false;
        }

        // Solo si el token se validó y resultó inválido. Si la característica
        // no está, el endpoint estaba exento (por ejemplo el hub de la cata).
        var validacion = contexto.Features.Get<IAntiforgeryValidationFeature>();
        if (validacion is null || validacion.IsValid)
        {
            return false;
        }

        // Solo navegación de páginas: una llamada de API o de SignalR debe
        // seguir recibiendo su 400.
        var aceptados = contexto.Request.Headers.Accept.ToString();
        return aceptados.Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }
}
