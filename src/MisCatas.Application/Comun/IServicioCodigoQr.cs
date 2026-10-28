namespace MisCatas.Application.Comun;

/// <summary>
/// Dibuja códigos QR. El resultado es un SVG, para que se vea nítido tanto en
/// el móvil como en una televisión de 55 pulgadas.
/// </summary>
public interface IServicioCodigoQr
{
    /// <summary>
    /// Devuelve el SVG del código QR de un texto, listo para incrustar en la
    /// página, o <c>null</c> si el texto está vacío o es demasiado largo.
    /// </summary>
    /// <param name="texto">Lo que se codifica (normalmente una dirección web).</param>
    /// <param name="colorModulos">Color de los cuadraditos, en formato CSS.</param>
    /// <param name="colorFondo">
    /// Color del fondo, en formato CSS. Conviene dejarlo blanco: los lectores
    /// necesitan contraste y muchos fallan con fondos oscuros o transparentes.
    /// </param>
    string? Svg(string? texto, string colorModulos = "#000000", string colorFondo = "#FFFFFF");
}
