using Microsoft.AspNetCore.Components;
using MisCatas.Application.Catas;
using MisCatas.Application.Usuarios;
using MisCatas.Application.Vinos;
using MisCatas.Domain.Enums;
using MisCatas.Web.Components.Shared.Iconos;
using MisCatas.Web.Seguridad;

namespace MisCatas.Web.Components.Pages.Admin;

public partial class Panel
{
    [Inject]
    private IServicioCatas ServicioCatas { get; set; } = default!;

    [Inject]
    private IServicioVinos ServicioVinos { get; set; } = default!;

    [Inject]
    private IServicioUsuarios ServicioUsuarios { get; set; } = default!;

    /// <summary>
    /// La única cata que puede estar activa (Abierta o EnCurso). Se consulta al
    /// entrar para que, si el administrador cerró la aplicación a mitad de una
    /// cata, se encuentre el aviso y el camino de vuelta al mando.
    /// </summary>
    private CataDto? CataActivaDto { get; set; }

    /// <summary>True cuando la cata activa ya se está votando.</summary>
    private bool CataActivaEnCurso => CataActivaDto?.Estado == EstadoCata.EnCurso;

    private int TotalCatas { get; set; }

    private int TotalVinos { get; set; }

    private int TotalUsuarios { get; set; }

    private IEnumerable<Cifra> Cifras =>
    [
        new(Icono.Copa, TotalCatas, "Catas"),
        new(Icono.Botella, TotalVinos, "Vinos"),
        new(Icono.Personas, TotalUsuarios, "Usuarios"),
    ];

    private static IEnumerable<Acceso> Accesos =>
    [
        new("Catas", "Crear, montar y lanzar catas", RutasApp.AdminCatas, Icono.Copa),
        new("Vinos", "Fichas, fotos y etiquetas", RutasApp.AdminVinos, Icono.Botella),
        new("Usuarios", "Invitados y permisos", RutasApp.AdminUsuarios, Icono.Personas),
        new("Ajustes", "Tiempos y reglas de votación", RutasApp.AdminAjustes, Icono.Ajustes),
    ];

    protected override async Task OnInitializedAsync()
    {
        // Una sola página de tamaño 1 basta: solo necesitamos los totales.
        var catas = await ServicioCatas.ListarAsync(tamanoPagina: 1);
        var vinos = await ServicioVinos.ListarAsync(tamanoPagina: 1);
        var usuarios = await ServicioUsuarios.ListarAsync(tamanoPagina: 1);

        TotalCatas = catas.TotalElementos;
        TotalVinos = vinos.TotalElementos;
        TotalUsuarios = usuarios.TotalElementos;

        // Abierta o EnCurso: en cualquiera de los dos casos hay una cata a
        // medias y el administrador tiene que saberlo nada más entrar.
        CataActivaDto = await ServicioCatas.ObtenerCataActivaAsync();
    }

    private sealed record Cifra(Icono Icono, int Valor, string Rotulo);

    private sealed record Acceso(string Titulo, string Pie, string Ruta, Icono Icono);
}
