using Microsoft.AspNetCore.Components;
using MisCatas.Application.Catas;
using MisCatas.Application.Comun;
using MisCatas.Domain.Enums;

namespace MisCatas.Web.Components.Pages.Admin;

public partial class Catas : IDisposable
{
    private const int TamanoPagina = 20;

    private static readonly TimeSpan RetardoBusqueda = TimeSpan.FromMilliseconds(350);

    private CancellationTokenSource? _temporizadorBusqueda;

    [Inject]
    private IServicioCatas Servicio { get; set; } = default!;

    private ResultadoPaginado<CataDto>? Datos { get; set; }

    private string Busqueda { get; set; } = string.Empty;

    private EstadoCata? Estado { get; set; }

    private int Pagina { get; set; } = 1;

    private bool Cargando { get; set; } = true;

    private string? Error { get; set; }

    private string? Mensaje { get; set; }

    private bool MostrarBorrado { get; set; }

    private CataDto? Candidato { get; set; }

    private string MensajeBorrado =>
        Candidato is null
            ? string.Empty
            : $"Se va a borrar «{Candidato.Nombre}». Esta acción no se puede deshacer.";

    /// <summary>Nombre amigable del estado para la interfaz.</summary>
    private static string NombreEstado(EstadoCata estado) => estado switch
    {
        EstadoCata.Borrador => "Borrador",
        EstadoCata.Abierta => "Abierta",
        EstadoCata.EnCurso => "En curso",
        EstadoCata.Finalizada => "Finalizada",
        _ => estado.ToString()
    };

    protected override Task OnInitializedAsync() => RecargarAsync();

    private async Task RecargarAsync()
    {
        Cargando = true;

        Datos = await Servicio.ListarAsync(Busqueda, Estado, Pagina, TamanoPagina);

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

    private async Task FiltrarEstadoAsync(ChangeEventArgs e)
    {
        var texto = e.Value?.ToString();

        Estado = Enum.TryParse<EstadoCata>(texto, out var valor) ? valor : null;
        Pagina = 1;

        await RecargarAsync();
    }

    private async Task IrAPaginaAsync(int pagina)
    {
        Pagina = pagina;

        await RecargarAsync();
    }

    private void PedirBorrado(CataDto cata)
    {
        Error = null;
        Mensaje = null;

        Candidato = cata;
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
            Mensaje = $"Cata «{Candidato.Nombre}» borrada.";
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
