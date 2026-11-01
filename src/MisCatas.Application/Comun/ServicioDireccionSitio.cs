using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Options;

namespace MisCatas.Application.Comun;

/// <summary>
/// Implementación de <see cref="IServicioDireccionSitio"/>.
///
/// La dirección de red local se busca UNA sola vez y se guarda: recorrer las
/// tarjetas de red en cada pintado de pantalla sería un gasto absurdo, y la IP
/// del equipo no cambia mientras la aplicación está en marcha.
/// </summary>
public class ServicioDireccionSitio : IServicioDireccionSitio
{
    /// <summary>Nombres de máquina que solo valen dentro del propio equipo.</summary>
    private static readonly string[] SoloEnEsteEquipo =
    [
        "localhost",
        "127.0.0.1",
        "::1",
        "[::1]",
        "0.0.0.0",
    ];

    private readonly IOptions<OpcionesSitio> _sitio;
    private readonly Lazy<string?> _ipRedLocal;

    public ServicioDireccionSitio(IOptions<OpcionesSitio> sitio)
    {
        ArgumentNullException.ThrowIfNull(sitio);

        _sitio = sitio;
        _ipRedLocal = new Lazy<string?>(BuscarIpRedLocal);
    }

    /// <inheritdoc />
    public string? Configurada => _sitio.Value.UrlPublicaLimpia;

    /// <inheritdoc />
    public string? EnEsteEquipo => _sitio.Value.UrlEnEsteEquipoLimpia;

    /// <inheritdoc />
    public string? IpRedLocal => _ipRedLocal.Value;

    /// <inheritdoc />
    public DireccionSitio Resolver(string? direccionNavegador)
    {
        // 1. Lo escrito a mano manda siempre: puede ser un dominio público o
        //    un puerto redirigido, cosas que no hay forma de deducir.
        if (Configurada is { } configurada)
        {
            return Componer(configurada, OrigenDireccionSitio.Configurada);
        }

        if (!Uri.TryCreate(direccionNavegador, UriKind.Absolute, out var url)
            || url.Scheme is not ("http" or "https"))
        {
            return Componer(null, OrigenDireccionSitio.Ninguna);
        }

        var puerto = url.IsDefaultPort ? string.Empty : $":{url.Port}";

        // 2. Si ya está entrando por una dirección que vale para cualquiera,
        //    se reparte esa misma.
        if (!EsSoloDeEsteEquipo(url.Host))
        {
            return Componer(
                $"{url.Scheme}://{url.Host}{puerto}",
                OrigenDireccionSitio.Navegador);
        }

        // 3. Ha entrado por «localhost»: se cambia el nombre por la dirección
        //    del equipo en la red, conservando esquema y puerto.
        if (IpRedLocal is { } ip)
        {
            return Componer(
                $"{url.Scheme}://{ip}{puerto}",
                OrigenDireccionSitio.RedLocal);
        }

        return Componer(null, OrigenDireccionSitio.Ninguna);
    }

    /// <summary>
    /// ¿Esta dirección solo sirve dentro del propio equipo?
    /// </summary>
    private static bool EsSoloDeEsteEquipo(string host) =>
        SoloEnEsteEquipo.Contains(host, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Busca la dirección IPv4 del equipo en la red local.
    ///
    /// Se descartan las tarjetas apagadas, la de bucle local y las de túnel, y
    /// se prefieren Wi-Fi y cable frente a adaptadores virtuales (los de
    /// máquinas virtuales y contenedores suelen quedar fuera del alcance de un
    /// móvil). Si algo falla, se devuelve <c>null</c> y la pantalla lo avisa.
    /// </summary>
    private static string? BuscarIpRedLocal()
    {
        try
        {
            var candidatas = new List<(int Prioridad, string Ip)>();

            foreach (var tarjeta in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (tarjeta.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                if (tarjeta.NetworkInterfaceType is NetworkInterfaceType.Loopback
                    or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var prioridad = tarjeta.NetworkInterfaceType switch
                {
                    NetworkInterfaceType.Wireless80211 => 0,
                    NetworkInterfaceType.Ethernet => 1,
                    NetworkInterfaceType.GigabitEthernet => 1,
                    _ => 2,
                };

                foreach (var direccion in tarjeta.GetIPProperties().UnicastAddresses)
                {
                    if (direccion.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    if (IPAddress.IsLoopback(direccion.Address))
                    {
                        continue;
                    }

                    var texto = direccion.Address.ToString();

                    // 169.254.x.x es lo que se asigna el equipo cuando no hay
                    // router: no lleva a ninguna parte.
                    if (texto.StartsWith("169.254.", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    candidatas.Add((prioridad, texto));
                }
            }

            return candidatas
                .OrderBy(c => c.Prioridad)
                .Select(c => c.Ip)
                .FirstOrDefault();
        }
        catch (NetworkInformationException)
        {
            // Sin acceso a la información de red: se queda sin deducir.
            return null;
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Monta el resultado pegándole la segunda dirección (la del nombre que
    /// solo existe en este equipo).
    ///
    /// Se hace en un sitio único porque hay cuatro salidas distintas en
    /// <see cref="Resolver"/> y antes era fácil olvidarse de una.
    /// </summary>
    private DireccionSitio Componer(string? raiz, OrigenDireccionSitio origen) =>
        new(raiz, origen) { SoloEnEsteEquipo = EnEsteEquipo };
}
