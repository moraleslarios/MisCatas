using System.Globalization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using MisCatas.Application.Imagenes;
using MisCatas.Application.Votacion;
using MisCatas.Domain.Enums;
using MisCatas.Infrastructure;
using MisCatas.Web.Components;
using MisCatas.Web.Endpoints;
using MisCatas.Web.Seguridad;
using MisCatas.Web.TiempoReal;

// Cultura española fija: notas con coma decimal y fechas dd/MM/yyyy.
var culturaEs = new CultureInfo("es-ES");
CultureInfo.DefaultThreadCurrentCulture = culturaEs;
CultureInfo.DefaultThreadCurrentUICulture = culturaEs;

var builder = WebApplication.CreateBuilder(args);

// HTTPS en la red local.
//
// Antes la aplicación se servía solo por HTTP, y eso daba dos problemas:
// los enlaces que se mandan por WhatsApp salían marcados como «no seguro» y
// algunos móviles los reescribían a https, rompiéndolos; y el QR llevaba a
// una dirección que el navegador trataba con desconfianza.
//
// Se escucha por los dos puertos: el de HTTP solo para redirigir a HTTPS, así
// quien tenga guardado el enlace viejo sigue llegando.
var certificado = CertificadoLocal.ObtenerOCrear(
    Path.Combine(builder.Environment.ContentRootPath, "certificados"));

builder.WebHost.ConfigureKestrel(opciones =>
{
    opciones.ListenAnyIP(PuertosApp.Http);

    opciones.ListenAnyIP(PuertosApp.Https, escucha =>
        escucha.UseHttps(certificado));
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Acceso a datos y servicios de negocio.
builder.Services.AgregarInfraestructura(builder.Configuration);

// Canal en vivo de la cata. Este registro va DESPUÉS de la infraestructura a
// propósito: sustituye al notificador nulo de reserva por el de SignalR (en
// la inyección de dependencias gana el último registro).
builder.Services.AddSignalR();
builder.Services.AddSingleton<BusCata>();
builder.Services.AddSingleton<INotificadorCata, NotificadorCataSignalR>();

// Conexión en vivo de cada pantalla. Scoped = una por circuito de Blazor,
// es decir, una por pestaña abierta.
builder.Services.AddScoped<ClienteCataHub>();

// Escucha de la cartelera: para las pantallas que esperan sin saber todavía
// qué cata se va a abrir (inicio del participante y escaparate ocioso).
builder.Services.AddScoped<ClienteCartelera>();

// Autenticación por cookie. Ya con HTTPS, pero marcando la cookie como
// segura solo cuando la petición venga cifrada: así la redirección inicial
// desde HTTP no se queda sin sesión a medio camino.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opciones =>
    {
        opciones.Cookie.Name = "MisCatas.Auth";
        opciones.Cookie.HttpOnly = true;
        opciones.Cookie.SameSite = SameSiteMode.Lax;
        opciones.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        opciones.LoginPath = RutasCuenta.Login;
        opciones.LogoutPath = RutasCuenta.Logout;
        opciones.AccessDeniedPath = RutasCuenta.AccesoDenegado;
        // La página de acceso lee «volverA», no el «ReturnUrl» por defecto:
        // sin esto, tras entrar desde un enlace protegido no se volvía al
        // destino original.
        opciones.ReturnUrlParameter = "volverA";
        opciones.ExpireTimeSpan = TimeSpan.FromDays(14);
        opciones.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(opciones =>
{
    opciones.AddPolicy(
        Politicas.SoloAdmin,
        politica => politica.RequireRole(nameof(Rol.Admin)));

    opciones.AddPolicy(
        Politicas.Votantes,
        politica => politica.RequireRole(nameof(Rol.Admin), nameof(Rol.Usuario)));

    opciones.AddPolicy(
        Politicas.Pantalla,
        politica => politica.RequireRole(nameof(Rol.Admin), nameof(Rol.Escaparate)));
});

// Expone el estado de autenticación a los componentes Blazor.
builder.Services.AddCascadingAuthenticationState();

// Límite de subida de imágenes coherente con el del servicio de imágenes.
builder.Services.Configure<FormOptions>(opciones =>
{
    opciones.MultipartBodyLengthLimit = IServicioImagenes.TamanoMaximoBytes;
});

// Claves de cifrado en disco y nombre de aplicación fijo.
//
// Sin esto, ASP.NET Core guarda las claves en un sitio volátil (perfil del
// usuario o memoria) y las regenera en cada arranque. Consecuencia: el token
// de antiforgery que llevaba una página ya abierta en un móvil deja de poder
// descifrarse tras reiniciar la app y el POST responde con
// «A valid antiforgery token was not provided with the request».
var carpetaClaves = new DirectoryInfo(
    Path.Combine(builder.Environment.ContentRootPath, "claves"));
carpetaClaves.Create();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(carpetaClaves)
    .SetApplicationName("MisCatas");

// Cookie de antiforgery explícita. «SameAsRequest» en vez de forzar Secure:
// durante el salto de HTTP a HTTPS conviven las dos, y marcarla siempre
// segura dejaba sin token a quien llegase por el enlace viejo.
builder.Services.AddAntiforgery(opciones =>
{
    opciones.Cookie.Name = "MisCatas.Antiforgery";
    opciones.Cookie.HttpOnly = true;
    opciones.Cookie.SameSite = SameSiteMode.Lax;
    opciones.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// Destino de la redirección a HTTPS. Hay que decirle el puerto a mano: los
// puertos de la aplicación no son los estándar (443), y sin esto la
// redirección llevaría a una dirección donde no hay nadie escuchando.
builder.Services.AddHttpsRedirection(opciones =>
{
    opciones.HttpsPort = PuertosApp.Https;
    opciones.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
});

var app = builder.Build();

// Todo lo que entre por HTTP se manda a HTTPS. El puerto va explícito porque
// no son los puertos estándar: sin decírselo, la redirección mandaría al 443.
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

// Red de seguridad: si el token de antiforgery ha caducado (pestaña vieja,
// vuelta atrás en el móvil o reinicio del servidor), se repinta la página con
// un token nuevo en vez de mostrar el error crudo. Va DESPUÉS de
// UseAntiforgery, que es quien deja el resultado de la validación.
app.UsarAntiforgeryTolerante();

app.MapStaticAssets();

// Las imágenes de los vinos se sirven por endpoint, nunca embebidas en el HTML.
app.MapEndpointsImagenes();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// WebSockets para el canal de la cata: con una sola instancia basta el hub
// autoalojado, sin backplane.
//
// DisableAntiforgery es imprescindible: UseAntiforgery valida todos los POST,
// y el «negotiate» de SignalR no lleva token (el hub de Blazor está exento
// por dentro). Sin esto, la negociación responde 400 y nadie se conecta. La
// seguridad la pone el [Authorize] del hub, que exige cookie de sesión.
app.MapHub<CataHub>(CataHub.Ruta).DisableAntiforgery();

app.Run();
