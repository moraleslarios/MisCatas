using Microsoft.AspNetCore.Components;
using MisCatas.Application.Catas;
using MisCatas.Application.Usuarios;
using MisCatas.Application.Vinos;
using MisCatas.Domain.Enums;
using MisCatas.Web.Components.Shared;
using System.Globalization;

namespace MisCatas.Web.Components.Pages.Admin;

public partial class CataMontar
{
    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    [Inject]
    private IServicioCatas Servicio { get; set; } = default!;

    [Inject]
    private IServicioVinos ServicioVinos { get; set; } = default!;

    [Inject]
    private IServicioUsuarios ServicioUsuarios { get; set; } = default!;

    /// <summary>Identificador de la cata que se está montando.</summary>
    [Parameter]
    public int Id { get; set; }

    private CataDetalleDto? Detalle { get; set; }

    private IReadOnlyList<VinoDto> TodosLosVinos { get; set; } = [];

    private IReadOnlyList<UsuarioDto> TodosLosUsuarios { get; set; } = [];

    private bool Cargando { get; set; } = true;

    private bool Trabajando { get; set; }

    private string? Error { get; set; }

    private string? Mensaje { get; set; }

    /// <summary>La cata solo se puede montar mientras no esté en curso ni finalizada.</summary>
    private bool Editable =>
        Detalle is not null
        && Detalle.Cata.Estado is EstadoCata.Borrador or EstadoCata.Abierta;

    /// <summary>Vinos del catálogo que todavía no están en la cata.</summary>
    private IEnumerable<VinoDto> VinosDisponibles
    {
        get
        {
            if (Detalle is null)
            {
                return [];
            }

            var yaPuestos = Detalle.Vinos.Select(v => v.VinoId).ToHashSet();

            return TodosLosVinos.Where(v => !yaPuestos.Contains(v.Id));
        }
    }

    /// <summary>Usuarios activos con rol Usuario que todavía no están invitados.</summary>
    private IEnumerable<UsuarioDto> UsuariosDisponibles
    {
        get
        {
            if (Detalle is null)
            {
                return [];
            }

            var yaInvitados = Detalle.Participantes.Select(p => p.UsuarioId).ToHashSet();

            // Solo votan los participantes normales: se excluyen los administradores
            // y el usuario escaparate.
            return TodosLosUsuarios
                .Where(u => u.Rol == Rol.Usuario && !yaInvitados.Contains(u.Id));
        }
    }

    /// <summary>Estados a los que se puede pasar desde el estado actual.</summary>
    private IEnumerable<EstadoCata> EstadosDestino => Detalle?.Cata.Estado switch
    {
        EstadoCata.Borrador => [EstadoCata.Abierta],
        EstadoCata.Abierta => [EstadoCata.Borrador, EstadoCata.EnCurso],
        EstadoCata.EnCurso => [EstadoCata.Finalizada],
        _ => []
    };

    private static string NombreEstado(EstadoCata estado) => estado switch
    {
        EstadoCata.Borrador => "Borrador",
        EstadoCata.Abierta => "Abierta",
        EstadoCata.EnCurso => "En curso",
        EstadoCata.Finalizada => "Finalizada",
        _ => estado.ToString()
    };

    private static string TextoTransicion(EstadoCata destino) => destino switch
    {
        EstadoCata.Borrador => "Volver a borrador",
        EstadoCata.Abierta => "Abrir inscripción",
        EstadoCata.EnCurso => "Empezar la cata",
        EstadoCata.Finalizada => "Finalizar la cata",
        _ => destino.ToString()
    };

    /// <summary>Pastilla de color según el estado de la cata.</summary>
    private static string ClasePastillaEstado(EstadoCata estado) => estado switch
    {
        EstadoCata.Borrador => "mc-pastilla-borrador",
        EstadoCata.Abierta => "mc-pastilla-abierta",
        EstadoCata.EnCurso => "mc-pastilla-encurso",
        _ => "mc-pastilla-finalizada"
    };

    /// <summary>El botón de la transición principal destaca; el resto es secundario.</summary>
    private static string ClaseTransicion(EstadoCata destino) => destino switch
    {
        EstadoCata.Borrador => "mc-boton mc-boton-fantasma",
        EstadoCata.EnCurso => "mc-boton mc-boton-dorado",
        EstadoCata.Finalizada => "mc-boton mc-boton-primario",
        _ => "mc-boton mc-boton-primario"
    };

    /// <summary>Fecha larga en castellano, con la primera letra en mayúscula.</summary>
    private string FechaLarga
    {
        get
        {
            if (Detalle is null)
            {
                return string.Empty;
            }

            var texto = Detalle.Cata.Fecha.ToString("dddd d 'de' MMMM 'de' yyyy", Espanol);

            return char.ToUpper(texto[0], Espanol) + texto[1..];
        }
    }

    /// <summary>Iniciales del participante para su ficha.</summary>
    private static string Iniciales(string nombreCompleto)
    {
        var trozos = nombreCompleto.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (trozos.Length == 0)
        {
            return "·";
        }

        return trozos.Length == 1
            ? trozos[0][..1].ToUpper(Espanol)
            : string.Concat(trozos[0][..1], trozos[1][..1]).ToUpper(Espanol);
    }

    /// <summary>Si está abierta la hoja de búsqueda de vinos.</summary>
    private bool BuscadorVinos { get; set; }

    /// <summary>Si está abierta la hoja de búsqueda de catadores.</summary>
    private bool BuscadorUsuarios { get; set; }

    /// <summary>Los vinos libres, traducidos a filas del buscador.</summary>
    private IReadOnlyList<OpcionSelector> OpcionesVinos =>
        [.. VinosDisponibles.Select(v => new OpcionSelector(
            v.Id,
            v.NombreConAnio,
            $"Lo trae {v.Dueno}",
            Detalle: v.Anio?.ToString(Espanol)))];

    /// <summary>Los catadores libres, traducidos a filas del buscador.</summary>
    private IReadOnlyList<OpcionSelector> OpcionesUsuarios =>
        [.. UsuariosDisponibles.Select(u => new OpcionSelector(
            u.Id,
            u.NombreCompleto,
            u.Login,
            Inicial: Iniciales(u.NombreCompleto)))];

    protected override async Task OnParametersSetAsync()
    {
        Cargando = true;
        Error = null;
        Mensaje = null;

        TodosLosVinos = await ServicioVinos.ListarTodosAsync();
        TodosLosUsuarios = await ServicioUsuarios.ListarActivosAsync();

        await RecargarAsync();

        Cargando = false;
    }

    private async Task RecargarAsync()
    {
        Detalle = await Servicio.ObtenerDetalleAsync(Id);
    }

    /// <summary>Ejecuta una operación del servicio mostrando su resultado.</summary>
    private async Task EjecutarAsync(
        Func<Task<Application.Comun.Resultado>> operacion,
        string mensajeExito)
    {
        Error = null;
        Mensaje = null;
        Trabajando = true;

        try
        {
            var resultado = await operacion();

            if (resultado.EsFallo)
            {
                Error = resultado.Error;
            }
            else
            {
                Mensaje = mensajeExito;
            }

            await RecargarAsync();
        }
        finally
        {
            Trabajando = false;
        }
    }

    private Task AgregarVinoAsync(int vinoId) =>
        EjecutarAsync(
            () => Servicio.AgregarVinoAsync(Id, vinoId),
            "Vino añadido a la cata.");

    private Task QuitarVinoAsync(int vinoId) =>
        EjecutarAsync(
            () => Servicio.QuitarVinoAsync(Id, vinoId),
            "Vino quitado de la cata.");

    private Task AgregarParticipanteAsync(int usuarioId) =>
        EjecutarAsync(
            () => Servicio.AgregarParticipanteAsync(Id, usuarioId),
            "Participante invitado.");

    private Task QuitarParticipanteAsync(int usuarioId) =>
        EjecutarAsync(
            () => Servicio.QuitarParticipanteAsync(Id, usuarioId),
            "Participante retirado.");

    /// <summary>Mueve un vino una posición arriba o abajo y reordena todo el recorrido.</summary>
    private Task MoverVinoAsync(int indice, int desplazamiento)
    {
        if (Detalle is null)
        {
            return Task.CompletedTask;
        }

        var destino = indice + desplazamiento;

        if (destino < 0 || destino >= Detalle.Vinos.Count)
        {
            return Task.CompletedTask;
        }

        var orden = Detalle.Vinos.Select(v => v.VinoId).ToList();

        (orden[indice], orden[destino]) = (orden[destino], orden[indice]);

        return EjecutarAsync(
            () => Servicio.ReordenarVinosAsync(Id, orden),
            "Orden actualizado.");
    }

    private Task CambiarEstadoAsync(EstadoCata destino) =>
        EjecutarAsync(
            () => Servicio.CambiarEstadoAsync(Id, destino),
            $"La cata pasa a «{NombreEstado(destino)}».");
}
