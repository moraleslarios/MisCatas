using Microsoft.AspNetCore.Components;
using MisCatas.Application.Configuraciones;
using MisCatas.Domain.Enums;

namespace MisCatas.Web.Components.Pages.Admin;

public partial class Ajustes
{
    [Inject]
    private IServicioConfiguracion Servicio { get; set; } = default!;

    /// <summary>Copia editable de la fila única de configuración.</summary>
    private Domain.Entidades.Configuracion? Datos { get; set; }

    private bool Guardando { get; set; }

    private string? Error { get; set; }

    private string? Mensaje { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Datos = await Servicio.ObtenerAsync();
    }

    private async Task GuardarAsync()
    {
        if (Datos is null)
        {
            return;
        }

        Error = null;
        Mensaje = null;
        Guardando = true;

        try
        {
            // Si la política no asigna nota fija, la nota por defecto no aplica.
            if (Datos.PoliticaNoVoto != PoliticaNoVoto.NotaFija)
            {
                Datos.NotaPorDefectoNoVoto = null;
            }

            var resultado = await Servicio.GuardarAsync(Datos);

            if (resultado.EsFallo)
            {
                Error = resultado.Error;
            }
            else
            {
                Mensaje = "Ajustes guardados.";
                Datos = await Servicio.ObtenerAsync();
            }
        }
        finally
        {
            Guardando = false;
        }
    }
}
