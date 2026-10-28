using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace MisCatas.Web.Components.Layout;

public partial class EscaparateLayout : LayoutComponentBase
{
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>
    /// Fecha del día en letra, como adorno de cabecera. No se refresca sola:
    /// la pantalla se recarga a diario y en la fase 3 habrá un reloj en vivo.
    /// </summary>
    private static string FechaLarga
    {
        get
        {
            var texto = DateTime.Now.ToString("dddd d 'de' MMMM 'de' yyyy", Espanol);
            return texto.Length > 0 ? char.ToUpper(texto[0], Espanol) + texto[1..] : texto;
        }
    }
}
