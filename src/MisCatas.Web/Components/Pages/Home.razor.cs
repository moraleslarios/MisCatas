using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MisCatas.Application.Participacion;
using MisCatas.Domain.Enums;
using MisCatas.Web.Seguridad;
using MisCatas.Web.TiempoReal;

namespace MisCatas.Web.Components.Pages;

public partial class Home : IDisposable
{
    [Inject]
    private IServicioParticipacion Servicio { get; set; } = default!;

    /// <summary>
    /// Escucha de la cartelera. El participante puede tener el móvil abierto
    /// en esta pantalla mucho antes de que el anfitrión arranque: sin esto
    /// tendría que recargar a mano para enterarse de que ya puede entrar.
    /// </summary>
    [Inject]
    private ClienteCartelera Cartelera { get; set; } = default!;

    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    [CascadingParameter]
    private Task<AuthenticationState>? EstadoAutenticacion { get; set; }

    private MisCatasDto Datos { get; set; } = MisCatasDto.Vacio;

    /// <summary>Identificador del usuario que ha entrado, para poder releer sus catas.</summary>
    private int? UsuarioId { get; set; }

    private bool Cargando { get; set; } = true;

    private string Saludo { get; set; } = "Hola";

    /// <summary>
    /// Falso para Admin y Escaparate: esos roles no catan, así que no se les
    /// enseña el hueco de «todavía no tienes catas» sino un aviso propio.
    /// </summary>
    private bool EsCatador { get; set; } = true;

    /// <summary>
    /// Cata ya abierta (pero sin empezar a votar) dentro de «Próximas». Si hay
    /// una, el participante tiene que saber que la cata está activa y que solo
    /// falta que el anfitrión la arranque.
    /// </summary>
    private CataUsuarioDto? CataAbierta =>
        Datos.Proximas.FirstOrDefault(c => c.Estado == EstadoCata.Abierta);

    /// <summary>
    /// Texto del aviso que se le da al participante cuando vuelve a entrar y
    /// su cata sigue viva: le dice que está en marcha y cómo volver a ella.
    /// </summary>
    private string TextoCataEnCurso
    {
        get
        {
            var cata = Datos.EnCurso.FirstOrDefault();

            if (cata is null)
            {
                return string.Empty;
            }

            var vuelta = cata.MisVotos > 0
                ? $"Ya has votado {cata.MisVotos} {(cata.MisVotos == 1 ? "vino" : "vinos")}."
                : "Todavía no has votado ningún vino.";

            return $"Tienes una cata en curso: «{cata.Nombre}». {vuelta} Pulsa «Entrar a votar» para volver a ella.";
        }
    }

    protected override async Task OnInitializedAsync()
    {
        if (EstadoAutenticacion is null)
        {
            Cargando = false;
            return;
        }

        var estado = await EstadoAutenticacion;
        var usuarioId = estado.User.ObtenerUsuarioId();

        Saludo = ConstruirSaludo(estado.User.ObtenerNombreCompleto());

        EsCatador = !estado.User.TieneRol(Rol.Admin)
                    && !estado.User.TieneRol(Rol.Escaparate);

        if (usuarioId is int id && EsCatador)
        {
            UsuarioId = id;

            // Se escucha ANTES de la primera lectura: si el anfitrión abre la
            // cata justo en ese hueco, el aviso ya tiene a quién llegar.
            Cartelera.Cambio += AlCambiarCarteleraAsync;
            Cartelera.Conectar();

            Datos = await Servicio.ObtenerMisCatasAsync(id);
        }

        Cargando = false;
    }

    /// <summary>
    /// La cata activa del sistema ha cambiado de situación. No se mira el
    /// identificador que llega por la cartelera: se vuelven a pedir LAS CATAS
    /// DE ESTE USUARIO, de modo que una cata a la que no está invitado no le
    /// mueve la pantalla ni le revela nada.
    /// </summary>
    private async Task AlCambiarCarteleraAsync(string evento, int cataId)
    {
        if (UsuarioId is not int id)
        {
            return;
        }

        Datos = await Servicio.ObtenerMisCatasAsync(id);

        // Si ya se vota en una cata suya, se le lleva directamente: es lo que
        // espera quien tiene el móvil en la mano esperando a que empiece.
        if (Datos.EnCurso.FirstOrDefault() is { } enCurso)
        {
            await InvokeAsync(() => Navegacion.NavigateTo(RutasApp.Votar(enCurso.Id)));
            return;
        }

        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        Cartelera.Cambio -= AlCambiarCarteleraAsync;
        Cartelera.Dispose();
    }

    /// <summary>
    /// Saludo corto con el nombre de pila, que es lo único que cabe bien en la
    /// cabecera de un móvil estrecho.
    /// </summary>
    private static string ConstruirSaludo(string nombreCompleto)
    {
        var nombre = nombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(nombre) ? "Hola" : $"Hola, {nombre}";
    }
}
