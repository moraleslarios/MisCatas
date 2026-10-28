using Microsoft.AspNetCore.Components;
using MisCatas.Application.Comun;
using MisCatas.Application.Vinos;

namespace MisCatas.Web.Components.Pages.Admin;

public partial class Vinos : IDisposable
{
    private const int TamanoPagina = 12;

    private static readonly TimeSpan RetardoBusqueda = TimeSpan.FromMilliseconds(350);

    private CancellationTokenSource? _temporizadorBusqueda;

    [Inject]
    private IServicioVinos Servicio { get; set; } = default!;

    private ResultadoPaginado<VinoDto>? Datos { get; set; }

    private string Busqueda { get; set; } = string.Empty;

    private int Pagina { get; set; } = 1;

    private bool Cargando { get; set; } = true;

    private string? Error { get; set; }

    private string? Mensaje { get; set; }

    private bool MostrarBorrado { get; set; }

    private VinoDto? Candidato { get; set; }

    private string MensajeBorrado =>
        Candidato is null
            ? string.Empty
            : $"Se va a borrar «{Candidato.NombreConAnio}». Esta acción no se puede deshacer.";

    protected override Task OnInitializedAsync() => RecargarAsync();

    private async Task RecargarAsync()
    {
        Cargando = true;

        Datos = await Servicio.ListarAsync(Busqueda, Pagina, TamanoPagina);

        Cargando = false;
    }

    private async Task BuscarAsync(ChangeEventArgs e)
    {
        Busqueda = e.Value?.ToString() ?? string.Empty;
        Pagina = 1;

        _temporizadorBusqueda?.Cancel();
        _temporizadorBusqueda?.Dispose();
        _temporizadorBusqueda = new CancellationTokenSource();

        try
        {
            await Task.Delay(RetardoBusqueda, _temporizadorBusqueda.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await RecargarAsync();
        StateHasChanged();
    }

    private async Task IrAPaginaAsync(int pagina)
    {
        Pagina = pagina;

        await RecargarAsync();
    }

    private void PedirBorrado(VinoDto vino)
    {
        Error = null;
        Mensaje = null;

        Candidato = vino;
        MostrarBorrado = true;
    }

    private async Task BorrarAsync()
    {
        if (Candidato is null)
        {
            return;
        }

        var resultado = await Servicio.EliminarAsync(Candidato.Id);

        if (resultado.EsFallo)
        {
            Error = resultado.Error;
        }
        else
        {
            Mensaje = $"Vino «{Candidato.NombreConAnio}» borrado.";
        }

        Candidato = null;

        await RecargarAsync();
    }

    public void Dispose()
    {
        _temporizadorBusqueda?.Cancel();
        _temporizadorBusqueda?.Dispose();
    }
}
