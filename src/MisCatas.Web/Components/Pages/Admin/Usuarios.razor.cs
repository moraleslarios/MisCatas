using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MisCatas.Application.Comun;
using MisCatas.Application.Usuarios;

namespace MisCatas.Web.Components.Pages.Admin;

public partial class Usuarios : IDisposable
{
    private const int TamanoPagina = 20;

    /// <summary>Retardo del filtro de búsqueda, para no consultar en cada tecla.</summary>
    private static readonly TimeSpan RetardoBusqueda = TimeSpan.FromMilliseconds(350);

    private CancellationTokenSource? _temporizadorBusqueda;

    [Inject]
    private IServicioUsuarios Servicio { get; set; } = default!;

    private ResultadoPaginado<UsuarioDto>? Datos { get; set; }

    private string Busqueda { get; set; } = string.Empty;

    private bool IncluirInactivos { get; set; } = true;

    private int Pagina { get; set; } = 1;

    private bool Cargando { get; set; } = true;

    private string? Error { get; set; }

    private string? Mensaje { get; set; }

    private bool MostrarBorrado { get; set; }

    private UsuarioDto? Candidato { get; set; }

    private string MensajeBorrado =>
        Candidato is null
            ? string.Empty
            : $"Se va a borrar «{Candidato.NombreCompleto}». Esta acción no se puede deshacer.";

    protected override Task OnInitializedAsync() => RecargarAsync();

    private async Task RecargarAsync()
    {
        Cargando = true;

        Datos = await Servicio.ListarAsync(
            Busqueda,
            Pagina,
            TamanoPagina,
            IncluirInactivos);

        Cargando = false;
    }

    private async Task BuscarAsync(ChangeEventArgs e)
    {
        Busqueda = e.Value?.ToString() ?? string.Empty;
        Pagina = 1;

        _temporizadorBusqueda?.Cancel();
        _temporizadorBusqueda?.Dispose();
        _temporizadorBusqueda = new CancellationTokenSource();

        var token = _temporizadorBusqueda.Token;

        try
        {
            await Task.Delay(RetardoBusqueda, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        await RecargarAsync();
        StateHasChanged();
    }

    private async Task CambiarInactivosAsync(ChangeEventArgs e)
    {
        IncluirInactivos = e.Value is true;
        Pagina = 1;

        await RecargarAsync();
    }

    private async Task IrAPaginaAsync(int pagina)
    {
        Pagina = pagina;

        await RecargarAsync();
    }

    private async Task ResetearAsync(UsuarioDto usuario)
    {
        LimpiarAvisos();

        var resultado = await Servicio.ResetearPasswordAsync(usuario.Id);

        if (resultado.EsFallo)
        {
            Error = resultado.Error;
            return;
        }

        Mensaje = $"La contraseña de «{usuario.NombreCompleto}» vuelve a ser su teléfono.";

        await RecargarAsync();
    }

    private async Task CambiarActivoAsync(UsuarioDto usuario)
    {
        LimpiarAvisos();

        var resultado = await Servicio.CambiarActivoAsync(usuario.Id, !usuario.Activo);

        if (resultado.EsFallo)
        {
            Error = resultado.Error;
            return;
        }

        Mensaje = usuario.Activo
            ? $"«{usuario.NombreCompleto}» ya no puede entrar."
            : $"«{usuario.NombreCompleto}» vuelve a estar activo.";

        await RecargarAsync();
    }

    private Task PedirBorradoAsync(UsuarioDto usuario)
    {
        LimpiarAvisos();

        Candidato = usuario;
        MostrarBorrado = true;

        return Task.CompletedTask;
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
            Mensaje = $"Usuario «{Candidato.NombreCompleto}» borrado.";
        }

        Candidato = null;

        await RecargarAsync();
    }

    private void LimpiarAvisos()
    {
        Error = null;
        Mensaje = null;
    }

    public void Dispose()
    {
        _temporizadorBusqueda?.Cancel();
        _temporizadorBusqueda?.Dispose();
    }
}
