using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MisCatas.Application.Autenticacion;
using MisCatas.Application.Avisos;
using MisCatas.Application.Catas;
using MisCatas.Application.Comun;
using MisCatas.Application.Configuraciones;
using MisCatas.Application.Imagenes;
using MisCatas.Application.Participacion;
using MisCatas.Application.Seguridad;
using MisCatas.Application.Usuarios;
using MisCatas.Application.Vinos;
using MisCatas.Application.Votacion;
using MisCatas.Infrastructure.Persistencia;
using MisCatas.Infrastructure.Servicios;

namespace MisCatas.Infrastructure;

/// <summary>Registro de la infraestructura en el contenedor de dependencias.</summary>
public static class InyeccionDependencias
{
    /// <summary>Nombre de la cadena de conexión en <c>appsettings.json</c>.</summary>
    public const string NombreCadenaConexion = "Default";

    /// <summary>
    /// Registra el acceso a datos y todos los servicios de la aplicación.
    /// </summary>
    public static IServiceCollection AgregarInfraestructura(
        this IServiceCollection servicios,
        IConfiguration configuracion)
    {
        ArgumentNullException.ThrowIfNull(servicios);
        ArgumentNullException.ThrowIfNull(configuracion);

        var cadena = configuracion.GetConnectionString(NombreCadenaConexion)
            ?? throw new InvalidOperationException(
                $"Falta la cadena de conexión «{NombreCadenaConexion}» en la configuración.");

        // Se usa una FÁBRICA, no un DbContext scoped: en Blazor Server varias
        // operaciones del mismo circuito pueden solaparse y DbContext no es
        // seguro para uso concurrente.
        servicios.AddDbContextFactory<AppDbContext>(opciones =>
            opciones.UseSqlServer(cadena, sql =>
            {
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
            }));

        // Opciones de procesado de imágenes.
        servicios.Configure<OpcionesImagenes>(
            configuracion.GetSection(OpcionesImagenes.Seccion));

        // Datos del sitio (dirección pública y prefijo telefónico). Hacen
        // falta para preparar los avisos de WhatsApp con un enlace que sí
        // resuelva desde el móvil de un invitado.
        servicios.Configure<OpcionesSitio>(
            configuracion.GetSection(OpcionesSitio.Seccion));

        // Servicios sin estado: una sola instancia basta.
        servicios.AddSingleton<IHasherContrasena, HasherContrasenaIdentity>();
        servicios.AddSingleton<IServicioImagenes, ServicioImagenes>();

        // Dirección por la que entran los invitados. Se deduce de la propia
        // navegación del organizador y solo se configura a mano en casos raros.
        servicios.AddSingleton<IServicioDireccionSitio, ServicioDireccionSitio>();

        // Dibujo de códigos QR en SVG. Sin estado y sin red.
        servicios.AddSingleton<IServicioCodigoQr, ServicioCodigoQr>();

        // Avisos de WhatsApp. Solo prepara el texto y el enlace: no envía nada
        // ni habla con ningún servicio externo.
        servicios.AddSingleton<IServicioAvisosWhatsApp, ServicioAvisosWhatsApp>();

        // Servicios de negocio. Son sin estado (crean su propio DbContext),
        // pero se registran como scoped por coherencia con el ciclo de la petición.
        servicios.AddScoped<IServicioUsuarios, ServicioUsuarios>();
        servicios.AddScoped<IServicioVinos, ServicioVinos>();
        servicios.AddScoped<IServicioCatas, ServicioCatas>();
        servicios.AddScoped<IServicioConfiguracion, ServicioConfiguracion>();
        servicios.AddScoped<IServicioAutenticacion, ServicioAutenticacion>();
        servicios.AddScoped<IServicioParticipacion, ServicioParticipacion>();

        // Resultados del concurso. Es el único sitio que decide quién gana
        // (media aritmética y desempate por menor desviación típica), para que
        // la clasificación sea idéntica en móvil, panel y escaparate.
        servicios.AddScoped<IServicioResultados, ServicioResultados>();

        // Motor de la cata en vivo. El estado de las catas activas es un
        // SINGLETON: es la única fuente de verdad del tiempo restante, y por
        // eso el reloj del servidor puede cerrar las votaciones por su cuenta.
        servicios.AddSingleton<EstadoCatasActivas>();

        // Notificador de reserva. La capa web lo sustituye por el de SignalR.
        servicios.AddSingleton<INotificadorCata, NotificadorCataNulo>();

        servicios.AddScoped<ServicioVotacion>();
        servicios.AddScoped<IServicioVotacion>(sp => sp.GetRequiredService<ServicioVotacion>());

        servicios.AddHostedService<RelojCatas>();

        return servicios;
    }

    /// <summary>
    /// Aplica las migraciones pendientes y crea los datos mínimos.
    /// Nunca borra datos existentes.
    /// </summary>
    public static async Task MigrarYSembrarAsync(
        this IServiceProvider proveedor,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proveedor);

        using var ambito = proveedor.CreateScope();

        var fabrica = ambito.ServiceProvider
            .GetRequiredService<IDbContextFactory<AppDbContext>>();
        var hasher = ambito.ServiceProvider.GetRequiredService<IHasherContrasena>();

        await using var db = await fabrica.CreateDbContextAsync(ct);

        await db.Database.MigrateAsync(ct);
        await SemillaDatos.EjecutarAsync(db, hasher, ct);
    }
}
