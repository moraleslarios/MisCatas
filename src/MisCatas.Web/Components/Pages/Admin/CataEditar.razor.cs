using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using MisCatas.Application.Catas;
using MisCatas.Web.Seguridad;

namespace MisCatas.Web.Components.Pages.Admin;

public partial class CataEditar
{
    [Inject]
    private IServicioCatas Servicio { get; set; } = default!;

    [Inject]
    private NavigationManager Navegacion { get; set; } = default!;

    /// <summary>Identificador de la cata a editar; nulo al crear.</summary>
    [Parameter]
    public int? Id { get; set; }

    private FormularioCata Datos { get; set; } = new();

    private bool EsNueva => Id is null;

    private bool NoEncontrada { get; set; }

    private bool Guardando { get; set; }

    private string? Error { get; set; }

    /// <summary>Texto del aviso de «guardado» que se muestra en la ventana.</summary>
    private string MensajeExito { get; set; } = string.Empty;

    /// <summary>La ventana de «guardado» está a la vista.</summary>
    private bool MostrarExito { get; set; }

    /// <summary>A dónde se va al cerrarse la ventana de «guardado».</summary>
    private string DestinoTrasGuardar { get; set; } = RutasApp.AdminCatas;

    protected override async Task OnParametersSetAsync()
    {
        NoEncontrada = false;
        Error = null;
        MostrarExito = false;

        if (Id is null)
        {
            Datos = new FormularioCata();
            return;
        }

        var detalle = await Servicio.ObtenerDetalleAsync(Id.Value);

        if (detalle is null)
        {
            NoEncontrada = true;
            return;
        }

        Datos = new FormularioCata
        {
            Nombre = detalle.Cata.Nombre,
            Lugar = detalle.Cata.Lugar,
            Fecha = detalle.Cata.Fecha,
            Comentarios = detalle.Cata.Comentarios,
            TiempoVotoSegundos = detalle.Cata.TiempoVotoSegundos
        };
    }

    private async Task GuardarAsync()
    {
        Error = null;
        MostrarExito = false;
        Guardando = true;

        try
        {
            var dto = new GuardarCataDto
            {
                Id = Id ?? 0,
                Nombre = Datos.Nombre,
                Lugar = Datos.Lugar,
                Fecha = Datos.Fecha,
                Comentarios = Datos.Comentarios,
                TiempoVotoSegundos = Datos.TiempoVotoSegundos
            };

            if (EsNueva)
            {
                var creada = await Servicio.CrearAsync(dto);

                if (creada.EsFallo)
                {
                    Error = creada.Error;
                    return;
                }

                // Tras crearla se va directo al montaje: vinos y participantes.
                MensajeExito = $"«{Datos.Nombre}» se ha creado. Ahora toca ponerle vinos y participantes.";
                DestinoTrasGuardar = $"/admin/catas/{creada.Valor}/montar";
                MostrarExito = true;
                return;
            }

            var editada = await Servicio.EditarAsync(dto);

            if (editada.EsFallo)
            {
                Error = editada.Error;
                return;
            }

            MensajeExito = $"Los cambios de «{Datos.Nombre}» se han guardado.";
            DestinoTrasGuardar = RutasApp.AdminCatas;
            MostrarExito = true;
        }
        finally
        {
            Guardando = false;
        }
    }

    /// <summary>Al cerrarse el aviso se vuelve a la pantalla que toque.</summary>
    private void SalirTrasGuardar()
    {
        MostrarExito = false;
        Navegacion.NavigateTo(DestinoTrasGuardar);
    }

    /// <summary>Datos del formulario con las validaciones de la interfaz.</summary>
    private sealed class FormularioCata
    {
        [Required(ErrorMessage = "El nombre de la cata es obligatorio.")]
        [StringLength(120, ErrorMessage = "El nombre no puede pasar de 120 caracteres.")]
        public string Nombre { get; set; } = string.Empty;

        [StringLength(120, ErrorMessage = "El lugar no puede pasar de 120 caracteres.")]
        public string? Lugar { get; set; }

        [Required(ErrorMessage = "La fecha es obligatoria.")]
        public DateTime Fecha { get; set; } = DateTime.Today;

        public string? Comentarios { get; set; }

        [Range(10, 3600, ErrorMessage = "El tiempo debe estar entre 10 y 3600 segundos.")]
        public int? TiempoVotoSegundos { get; set; }
    }
}
