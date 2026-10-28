using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MisCatas.Web.Components.Shared.Iconos;
using MisCatas.Web.Seguridad;

namespace MisCatas.Web.Components.Layout;

public partial class AdminLayout : LayoutComponentBase
{
    /// <summary>
    /// Las cinco secciones del panel. Se pintan en la barra de escritorio y en
    /// la barra inferior de móvil, así no hay dos listas que se desincronicen.
    /// </summary>
    private static readonly EnlaceAdmin[] Enlaces =
    [
        new("Panel", "Panel", RutasApp.AdminInicio, Icono.Casa, NavLinkMatch.All),
        new("Catas", "Catas", RutasApp.AdminCatas, Icono.Copa, NavLinkMatch.Prefix),
        new("Vinos", "Vinos", RutasApp.AdminVinos, Icono.Botella, NavLinkMatch.Prefix),
        new("Usuarios", "Gente", RutasApp.AdminUsuarios, Icono.Personas, NavLinkMatch.Prefix),
        new("Ajustes", "Ajustes", RutasApp.AdminAjustes, Icono.Ajustes, NavLinkMatch.Prefix),
    ];

    private sealed record EnlaceAdmin(
        string Titulo,
        string TituloCorto,
        string Ruta,
        Icono Icono,
        NavLinkMatch Coincidencia);
}
