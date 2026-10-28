namespace MisCatas.Application.Imagenes;

/// <summary>
/// Imagen ya normalizada y lista para guardar en la base de datos.
/// </summary>
/// <param name="Contenido">Bytes del JPEG resultante.</param>
/// <param name="ContentType">Tipo MIME, siempre <c>image/jpeg</c>.</param>
/// <param name="Ancho">Ancho final en píxeles.</param>
/// <param name="Alto">Alto final en píxeles.</param>
public record ImagenProcesada(
    byte[] Contenido,
    string ContentType,
    int Ancho,
    int Alto);
