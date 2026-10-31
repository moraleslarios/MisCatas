using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using MisCatas.Application.Participacion;
using MisCatas.Domain.Enums;
using MisCatas.Web.Components.Shared.Iconos;
using MisCatas.Web.Seguridad;
using MisCatas.Web.TiempoReal;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Vigilante silencioso que avisa al catador, esté en la pantalla que esté,
/// de que el anfitrión ha abierto (o arrancado) una cata en la que participa.
///
/// Va colgado del diseño general, así que escucha durante toda la sesión. No
/// transporta nada de la cata por el canal en vivo: cuando llega el aviso
/// vuelve a pedir LAS CATAS DE ESTE USUARIO, de modo que una cata a la que no
/// está invitado no le enseña nada.
///
/// Se engancha al bus DIRECTAMENTE en vez de usar ClienteCartelera porque
/// ese servicio es uno por pestaña y las pantallas que lo usan lo liberan al
/// abandonarlas: compartirlo dejaría este aviso sordo en cuanto el catador
/// saliese de la pantalla principal.
/// </summary>
public partial class AvisoCataAbierta : IOyenteCartelera, IDisposable
{
    /// <summary>Catas de la misma apertura que ya se le han enseñado, para no repetir.</summary>
    private readonly HashSet<int> _yaAvisadas = [];

    private ElementReference _boton;
    private bool _enfocar;
    private IDisposable? _suscripcion;

    [Inject]
    private IServicioParticipacion Servicio { get; set; } = default!;

    [Inject]
    private BusCata Bus { get; set; } = default!;

    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? EstadoAutenticacion { get; set; }

    /// <summary>Usuario que ha entrado. Null para Admin, Escaparate o anónimo.</summary>
    private int? UsuarioId { get; set; }

    /// <summary>La ventana del aviso está a la vista.</summary>
    private bool Visible { get; set; }

    /// <summary>Cata de la que se está avisando.</summary>
    private CataUsuarioDto? Cata { get; set; }

    /// <summary>Icono de la cabecera: copa si ya se vota, reloj si solo está abierta.</summary>
    private Icono IconoCabecera =>
        Cata?.Estado == EstadoCata.EnCurso ? Icono.Copa : Icono.Reloj;

    private string Antetitulo =>
        Cata?.Estado == EstadoCata.EnCurso ? "¡Ya se está votando!" : "El anfitrión ha abierto la cata";

    private string Mensaje =>
        Cata?.Estado == EstadoCata.EnCurso
            ? "La votación está abierta. Entra ya para no quedarte atrás con los vinos."
            : "Todavía no se vota. Entra y espera a que el anfitrión arranque el primer vino.";

    private string TextoBoton =>
        Cata?.Estado == EstadoCata.EnCurso ? "Entrar a votar" : "Entrar a la cata";

    protected override async Task OnInitializedAsync()
    {
        if (EstadoAutenticacion is null)
        {
            return;
        }

        var estado = await EstadoAutenticacion;

        // Admin y Escaparate no catan: a ellos este aviso solo molestaría.
        if (estado.User.TieneRol(Rol.Admin) || estado.User.TieneRol(Rol.Escaparate))
        {
            return;
        }

        if (estado.User.ObtenerUsuarioId() is not int id)
        {
            return;
        }

        UsuarioId = id;

        // Lo que ya está en marcha al entrar NO se avisa: el catador acaba de
        // cargar la pantalla y ya lo ve. El aviso es para lo que pase después.
        foreach (var cata in await LeerCandidatasAsync(id))
        {
            _yaAvisadas.Add(cata.Id);
        }

        _suscripcion = Bus.SuscribirCartelera(this);
    }

    protected override async Task OnAfterRenderAsync(bool primeraVez)
    {
        if (_enfocar)
        {
            _enfocar = false;

            try
            {
                await _boton.FocusAsync();
            }
            catch
            {
                // Si el botón ya no está en pantalla no pasa nada.
            }
        }
    }

    /// <summary>
    /// Ha cambiado la cartelera. Se releen las catas del usuario y, si hay
    /// alguna recién abierta de la que no se le haya avisado, se le enseña.
    /// </summary>
    public async Task RecibirCarteleraAsync(string evento, int cataId)
    {
        if (UsuarioId is not int id)
        {
            return;
        }

        var candidatas = await LeerCandidatasAsync(id);

        var nueva = candidatas.FirstOrDefault(c => !_yaAvisadas.Contains(c.Id));

        // Lo que ya no está vivo se olvida, para que una segunda apertura de
        // la misma cata vuelva a avisar.
        _yaAvisadas.RemoveWhere(x => candidatas.All(c => c.Id != x));

        if (nueva is null)
        {
            return;
        }

        _yaAvisadas.Add(nueva.Id);

        if (DebeCallarse(nueva))
        {
            return;
        }

        Cata = nueva;
        Visible = true;
        _enfocar = true;

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Catas del usuario que están abiertas o ya votándose.</summary>
    private async Task<IReadOnlyList<CataUsuarioDto>> LeerCandidatasAsync(int usuarioId)
    {
        var datos = await Servicio.ObtenerMisCatasAsync(usuarioId);

        return
        [
            .. datos.EnCurso,
            .. datos.Proximas.Where(c => c.Estado == EstadoCata.Abierta)
        ];
    }

    /// <summary>
    /// Hay dos situaciones en las que el aviso solo estorbaría: cuando el
    /// catador ya está dentro de esa misma cata (lo tiene delante y se le
    /// actualiza solo) y cuando está en «Mis catas» y la cata ya se está
    /// votando, porque esa pantalla lo mete directamente a votar.
    /// </summary>
    private bool DebeCallarse(CataUsuarioDto cata)
    {
        var ruta = RutaActual();

        if (ruta.StartsWith($"/catas/{cata.Id}/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ruta == "/" && cata.Estado == EstadoCata.EnCurso;
    }

    /// <summary>Ruta actual, sin la barra final ni la parte de consulta.</summary>
    private string RutaActual()
    {
        var relativa = Navegacion.ToBaseRelativePath(Navegacion.Uri).Split('?')[0];

        var limpia = ("/" + relativa).TrimEnd('/');

        return limpia.Length == 0 ? "/" : limpia;
    }

    /// <summary>Se entra a la cata avisada.</summary>
    private Task IrAsync()
    {
        if (Cata is not { } cata)
        {
            return Task.CompletedTask;
        }

        Visible = false;

        var destino = cata.Estado == EstadoCata.EnCurso
            ? RutasApp.Votar(cata.Id)
            : RutasApp.Resumen(cata.Id);

        Navegacion.NavigateTo(destino);

        return Task.CompletedTask;
    }

    /// <summary>Se cierra el aviso sin ir a ninguna parte.</summary>
    private Task PosponerAsync()
    {
        Visible = false;
        return Task.CompletedTask;
    }

    private Task AlPulsarTeclaAsync(KeyboardEventArgs e) =>
        e.Key == "Escape" ? PosponerAsync() : Task.CompletedTask;

    public void Dispose()
    {
        _suscripcion?.Dispose();
        _suscripcion = null;
    }
}
