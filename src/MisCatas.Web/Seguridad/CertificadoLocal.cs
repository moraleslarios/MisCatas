using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MisCatas.Web.Seguridad;

/// <summary>
/// Certificado para servir la aplicación por HTTPS en la red de casa.
///
/// Por qué existe esto: el certificado de desarrollo de .NET
/// (<c>dotnet dev-certs</c>) solo vale para «localhost», así que al entrar
/// desde un móvil por la dirección IP del equipo el navegador no solo avisa,
/// directamente no encuentra el nombre en el certificado. Aquí se fabrica uno
/// propio que incluye «localhost», el nombre del equipo y TODAS sus
/// direcciones de red, que es lo que se reparte por WhatsApp y en el QR.
///
/// El certificado se guarda en disco y se reutiliza: si se regenerase en cada
/// arranque, cada móvil tendría que volver a aceptar el aviso.
///
/// Aviso honesto: al no estar firmado por una autoridad conocida, la primera
/// vez cada dispositivo muestra un «la conexión no es privada». Se acepta una
/// vez y queda recordado. Para quitarlo del todo se puede instalar el fichero
/// <c>miscatas-local.crt</c> (que se deja junto al certificado) como
/// certificado de confianza en el teléfono.
/// </summary>
public static class CertificadoLocal
{
    /// <summary>Nombre del almacén con la clave privada, para el servidor.</summary>
    private const string NombrePfx = "miscatas-local.pfx";

    /// <summary>Nombre de la copia pública, para instalar en los móviles.</summary>
    private const string NombreCrt = "miscatas-local.crt";

    /// <summary>
    /// Días de validez del certificado.
    /// </summary>
    /// <remarks>
    /// 398 días no es un número caprichoso: es el máximo que aceptan Safari y
    /// iOS. Un certificado de servidor que dure más lo descartan por norma, y
    /// lo peor es CÓMO lo descartan: el iPhone corta la conexión sin ofrecer
    /// siquiera la opción de confiar en el sitio, así que la web resultaba
    /// inalcanzable desde cualquier iPhone de la casa. Antes esto duraba cinco
    /// años «para no tener que renovar», y precisamente por eso no funcionaba.
    ///
    /// Con la renovación automática de más abajo, que un año pase volando da
    /// igual: el certificado se rehace solo cuando le quedan pocos días.
    /// </remarks>
    private const int DiasValidez = 397;

    /// <summary>
    /// Margen por debajo del cual se considera que toca renovar. Evita que el
    /// certificado caduque justo en mitad de una cata.
    /// </summary>
    private static readonly TimeSpan MargenRenovacion = TimeSpan.FromDays(30);

    /// <summary>
    /// Devuelve el certificado guardado o fabrica uno nuevo si no hay, si ha
    /// caducado o si le falta alguna de las direcciones actuales del equipo
    /// (por ejemplo, porque el router ha dado otra IP).
    /// </summary>
    /// <param name="carpeta">Carpeta donde guardarlo.</param>
    public static X509Certificate2 ObtenerOCrear(string carpeta)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(carpeta);

        Directory.CreateDirectory(carpeta);

        var rutaPfx = Path.Combine(carpeta, NombrePfx);
        var nombres = NombresDelEquipo();

        if (TryCargar(rutaPfx, nombres, out var guardado))
        {
            ConfiarEnEstePc(guardado);

            return guardado;
        }

        var nuevo = Fabricar(nombres);

        Guardar(nuevo, carpeta, rutaPfx);
        ConfiarEnEstePc(nuevo);

        return nuevo;
    }

    /// <summary>
    /// Intenta reaprovechar el certificado del disco. Cualquier problema
    /// (fichero corrupto, sin permisos, direcciones distintas) se traduce en
    /// «no vale», y se fabricará otro.
    /// </summary>
    private static bool TryCargar(
        string rutaPfx,
        IReadOnlyCollection<string> nombres,
        out X509Certificate2 certificado)
    {
        certificado = null!;

        if (!File.Exists(rutaPfx))
        {
            return false;
        }

        try
        {
            var candidato = X509CertificateLoader.LoadPkcs12FromFile(
                rutaPfx,
                password: null,
                keyStorageFlags: X509KeyStorageFlags.Exportable);

            var vigente = candidato.NotAfter - DateTime.Now > MargenRenovacion
                && candidato.NotBefore <= DateTime.Now;

            // Un certificado demasiado largo no sirve, aunque esté en vigor:
            // iOS rechaza los que duran más de 398 días y encima no deja
            // confiar en ellos a mano. Este repaso es el que jubila a los
            // certificados de cinco años que se fabricaron antes de saberlo;
            // sin él se reaprovecharían tal cual y el iPhone seguiría sin
            // poder entrar.
            var duracionAceptable =
                (candidato.NotAfter - candidato.NotBefore).TotalDays <= DiasValidez;

            if (vigente && duracionAceptable && CubreTodos(candidato, nombres))
            {
                certificado = candidato;
                return true;
            }

            candidato.Dispose();

            return false;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// ¿El certificado incluye todas las direcciones y nombres por los que se
    /// puede llegar hoy al equipo?
    /// </summary>
    private static bool CubreTodos(
        X509Certificate2 certificado,
        IReadOnlyCollection<string> nombres)
    {
        var cubiertos = certificado.Extensions
            .OfType<X509SubjectAlternativeNameExtension>()
            .SelectMany(e => e.EnumerateDnsNames()
                .Concat(e.EnumerateIPAddresses().Select(ip => ip.ToString())))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return nombres.All(cubiertos.Contains);
    }

    /// <summary>
    /// Fabrica un certificado autofirmado válido para todos los nombres y
    /// direcciones del equipo.
    /// </summary>
    private static X509Certificate2 Fabricar(IReadOnlyCollection<string> nombres)
    {
        using var clave = RSA.Create(2048);

        var peticion = new CertificateRequest(
            new X500DistinguishedName("CN=MisCatas (red local), O=MisCatas"),
            clave,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // Certificado de servidor, no de autoridad.
        peticion.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(
                certificateAuthority: false,
                hasPathLengthConstraint: false,
                pathLengthConstraint: 0,
                critical: true));

        peticion.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                critical: true));

        // «Autenticación de servidor web»: sin este uso, Chrome y Android
        // rechazan el certificado incluso aunque se acepte el aviso.
        peticion.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")],
                critical: false));

        var alternativos = new SubjectAlternativeNameBuilder();

        foreach (var nombre in nombres)
        {
            if (IPAddress.TryParse(nombre, out var ip))
            {
                alternativos.AddIpAddress(ip);
            }
            else
            {
                alternativos.AddDnsName(nombre);
            }
        }

        peticion.CertificateExtensions.Add(alternativos.Build());

        // Un día de margen hacia atrás: evita sustos si el reloj del móvil va
        // ligeramente adelantado respecto al del servidor.
        //
        // El total (margen + validez) tiene que quedar por debajo de los 398
        // días que tolera iOS, por eso el margen se descuenta de la validez en
        // lugar de sumarse.
        using var recienHecho = peticion.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1),
            DateTimeOffset.Now.AddDays(DiasValidez - 1));

        // Paso obligado por PFX. El certificado que sale de CreateSelfSigned
        // lleva la clave privada «en el aire» (efímera), y Windows no acepta
        // esas claves para cifrar una conexión: el servidor arranca, dice que
        // escucha, y luego corta todas las conexiones sin dar explicaciones.
        // Exportar e importar la convierte en una clave de verdad.
        return X509CertificateLoader.LoadPkcs12(
            recienHecho.Export(X509ContentType.Pfx),
            password: null,
            keyStorageFlags: X509KeyStorageFlags.Exportable);
    }

    /// <summary>
    /// Deja en disco el almacén con la clave privada (lo usa Kestrel) y una
    /// copia pública en formato <c>.crt</c> (para instalar en los móviles).
    /// Si no se puede escribir, la aplicación sigue adelante con el
    /// certificado en memoria.
    /// </summary>
    private static void Guardar(
        X509Certificate2 certificado,
        string carpeta,
        string rutaPfx)
    {
        try
        {
            File.WriteAllBytes(
                rutaPfx,
                certificado.Export(X509ContentType.Pfx));

            File.WriteAllBytes(
                Path.Combine(carpeta, NombreCrt),
                certificado.Export(X509ContentType.Cert));
        }
        catch (IOException)
        {
            // Sin persistencia: cada arranque pedirá aceptar el aviso de nuevo.
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (CryptographicException)
        {
        }
    }

    /// <summary>
    /// Mete el certificado entre los de confianza del usuario de este equipo.
    /// Así en el PC donde corre la aplicación no sale ningún aviso; en los
    /// móviles sigue saliendo la primera vez (ahí hay que instalar a mano el
    /// fichero <c>miscatas-local.crt</c> si se quiere evitar).
    ///
    /// Se usa el almacén del usuario y no el de la máquina a propósito: el de
    /// la máquina exige permisos de administrador, y arrancar la aplicación
    /// como administrador para esto sería desproporcionado.
    /// </summary>
    private static void ConfiarEnEstePc(X509Certificate2 certificado)
    {
        try
        {
            using var almacen = new X509Store(StoreName.Root, StoreLocation.CurrentUser);

            almacen.Open(OpenFlags.ReadWrite);

            // Se limpian los de arranques anteriores: si no, el almacén se va
            // llenando de certificados caducados de MisCatas.
            var viejos = almacen.Certificates
                .Where(c => c.Subject.Contains("MisCatas", StringComparison.OrdinalIgnoreCase)
                    && c.Thumbprint != certificado.Thumbprint)
                .ToArray();

            foreach (var viejo in viejos)
            {
                almacen.Remove(viejo);
            }

            almacen.Add(certificado);
        }
        catch (CryptographicException)
        {
            // Sin confianza instalada: la aplicación funciona igual, solo que
            // el navegador de este PC mostrará el aviso.
        }
        catch (UnauthorizedAccessException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }
    }

    /// <summary>
    /// Todos los nombres por los que se puede llamar a este equipo:
    /// «localhost», el bucle local, el nombre de máquina y cada dirección
    /// IPv4 de sus tarjetas de red encendidas.
    /// </summary>
    private static List<string> NombresDelEquipo()
    {
        var nombres = new List<string> { "localhost", "127.0.0.1", "::1" };

        try
        {
            var equipo = Dns.GetHostName();

            if (!string.IsNullOrWhiteSpace(equipo))
            {
                nombres.Add(equipo);
            }
        }
        catch (SocketException)
        {
        }

        try
        {
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

                foreach (var direccion in tarjeta.GetIPProperties().UnicastAddresses)
                {
                    if (direccion.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    var texto = direccion.Address.ToString();

                    // 169.254.x.x es la que se inventa Windows cuando no hay
                    // router: no lleva a ninguna parte.
                    if (texto.StartsWith("169.254.", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    nombres.Add(texto);
                }
            }
        }
        catch (NetworkInformationException)
        {
        }
        catch (PlatformNotSupportedException)
        {
        }

        return [.. nombres.Distinct(StringComparer.OrdinalIgnoreCase)];
    }
}
