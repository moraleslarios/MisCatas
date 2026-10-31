namespace MisCatas.Web.Seguridad;

/// <summary>
/// Dice cómo se está ejecutando la aplicación: suelta (con su propio servidor
/// web y sus puertos 5443/5080) o dentro de un IIS local.
///
/// Hace falta distinguirlo porque las dos formas se pisan:
/// cuando IIS hospeda la web, es IIS quien abre los puertos y quien pone el
/// certificado, así que si la aplicación insistiera en abrir el 5443 por su
/// cuenta tendríamos dos servidores peleándose por lo mismo, y la redirección
/// «de HTTP a HTTPS» mandaría al visitante al puerto 5443 en lugar de al 443,
/// que es donde IIS escucha de verdad.
/// </summary>
public static class Hospedaje
{
    /// <summary>
    /// Clave de configuración para forzar el modo IIS a mano, por si algún día
    /// la detección automática no acierta: <c>"Hospedaje:Iis": true</c> en el
    /// appsettings, o el argumento <c>--Hospedaje:Iis true</c> al arrancar.
    /// </summary>
    public const string ClaveForzarIis = "Hospedaje:Iis";

    /// <summary>
    /// Variables de entorno que IIS inyecta al proceso de la aplicación a
    /// través de su módulo de ASP.NET Core. Si existe cualquiera de ellas,
    /// estamos dentro de IIS. No se usa una sola porque cambian según el modo
    /// de hospedaje (dentro del propio proceso o en un proceso aparte) y según
    /// si el sitio tiene enlace HTTPS configurado.
    /// </summary>
    private static readonly string[] SenalesDeIis =
    [
        "ASPNETCORE_IIS_PHYSICAL_PATH",
        "ASPNETCORE_IIS_HTTPS_PORT",
        "ASPNETCORE_ANCM_HTTPS_PORT",
        "ASPNETCORE_IIS_HTTPAUTH",
        "ASPNETCORE_TOKEN"
    ];

    /// <summary>
    /// <c>true</c> cuando la web la está sirviendo un IIS. En ese caso la
    /// aplicación no debe tocar puertos, certificados ni redirecciones: de eso
    /// se encarga IIS con la configuración del sitio.
    /// </summary>
    public static bool EsIis(IConfiguration configuracion)
    {
        if (configuracion.GetValue<bool>(ClaveForzarIis))
        {
            return true;
        }

        foreach (var variable in SenalesDeIis)
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(variable)))
            {
                return true;
            }
        }

        return false;
    }
}
