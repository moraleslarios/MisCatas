using Microsoft.AspNetCore.Mvc;
using MisCatas.Application.Vinos;

namespace MisCatas.Web.Endpoints;

/// <summary>
/// Endpoints que sirven las imágenes de los vinos guardadas en la base de datos.
/// Nunca se embeben en el HTML: así el navegador las cachea.
/// </summary>
public static class EndpointsImagenes
{
    public static IEndpointRouteBuilder MapEndpointsImagenes(this IEndpointRouteBuilder rutas)
    {
        ArgumentNullException.ThrowIfNull(rutas);

        var grupo = rutas.MapGroup("/imagenes/vinos").RequireAuthorization();

        grupo.MapGet("/{id:int}/foto", (
            int id,
            [FromServices] IServicioVinos servicio,
            CancellationToken ct) =>
            DevolverAsync(servicio, id, TipoImagenVino.Foto, ct));

        grupo.MapGet("/{id:int}/etiqueta", (
            int id,
            [FromServices] IServicioVinos servicio,
            CancellationToken ct) =>
            DevolverAsync(servicio, id, TipoImagenVino.Etiqueta, ct));

        return rutas;
    }

    private static async Task<IResult> DevolverAsync(
        IServicioVinos servicio,
        int vinoId,
        TipoImagenVino tipo,
        CancellationToken ct)
    {
        var imagen = await servicio.ObtenerImagenAsync(vinoId, tipo, ct);

        if (imagen is null)
        {
            return Results.NotFound();
        }

        // ETag débil basado en el tamaño: cambia al sustituir la imagen.
        var etag = $"\"{vinoId}-{(int)tipo}-{imagen.Contenido.Length}\"";

        return Results.File(
            imagen.Contenido,
            contentType: imagen.ContentType,
            entityTag: new Microsoft.Net.Http.Headers.EntityTagHeaderValue(etag));
    }
}
