using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MisCatas.Web.Components.Shared.Iconos;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Aviso de «hecho» en forma de ventana sobre la pantalla. Se cierra solo
/// pasados <see cref="SegundosAutocierre"/> segundos y avisa con
/// <see cref="AlCerrar"/>, que es donde la pantalla suele volver al listado.
/// </summary>
public partial class ModalExito : IDisposable
{
    private readonly string _id = Guid.NewGuid().ToString("N")[..8];

    private ElementReference _boton;
    private bool _visibleAnterior;
    private bool _cerrando;
    private CancellationTokenSource? _temporizador;

    /// <summary>Está a la vista.</summary>
    [Parameter]
    public bool Visible { get; set; }

    [Parameter]
    public EventCallback<bool> VisibleChanged { get; set; }

    [Parameter]
    public string Titulo { get; set; } = "Hecho";

    [Parameter]
    public string Mensaje { get; set; } = string.Empty;

    [Parameter]
    public RenderFragment? Detalle { get; set; }

    [Parameter]
    public string TextoBoton { get; set; } = "Continuar";

    [Parameter]
    public Icono? Icono { get; set; }

    /// <summary>
    /// Segundos que se queda a la vista antes de cerrarse solo. Con 0 o menos
    /// no se cierra automáticamente y hay que pulsar el botón.
    /// </summary>
    [Parameter]
    public int SegundosAutocierre { get; set; } = 2;

    /// <summary>Se dispara justo después de cerrarse, por cualquier vía.</summary>
    [Parameter]
    public EventCallback AlCerrar { get; set; }

    private Icono IconoCabecera => Icono ?? Iconos.Icono.Hecho;

    protected override async Task OnAfterRenderAsync(bool primeraVez)
    {
        if (Visible && !_visibleAnterior)
        {
            _visibleAnterior = true;
            _cerrando = false;

            await _boton.FocusAsync();

            if (SegundosAutocierre > 0)
            {
                ArrancarTemporizador();
            }
        }
        else if (!Visible && _visibleAnterior)
        {
            _visibleAnterior = false;
            PararTemporizador();
        }
    }

    /// <summary>
    /// Espera en segundo plano y cierra. No se usa JavaScript: el reloj vive
    /// en el servidor, igual que el resto de la aplicación.
    /// </summary>
    private void ArrancarTemporizador()
    {
        PararTemporizador();

        _temporizador = new CancellationTokenSource();
        var testigo = _temporizador.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(SegundosAutocierre), testigo);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (testigo.IsCancellationRequested)
            {
                return;
            }

            await InvokeAsync(CerrarAsync);
        });
    }

    private void PararTemporizador()
    {
        if (_temporizador is null)
        {
            return;
        }

        _temporizador.Cancel();
        _temporizador.Dispose();
        _temporizador = null;
    }

    private async Task AlPulsarTeclaAsync(KeyboardEventArgs argumentos)
    {
        if (argumentos.Key is "Escape" or "Esc" or "Enter" or " ")
        {
            await CerrarAsync();
        }
    }

    private async Task CerrarAsync()
    {
        // El autocierre y el botón pueden coincidir: solo se cierra una vez.
        if (_cerrando || !Visible)
        {
            return;
        }

        _cerrando = true;
        PararTemporizador();

        if (VisibleChanged.HasDelegate)
        {
            await VisibleChanged.InvokeAsync(false);
        }
        else
        {
            Visible = false;
        }

        if (AlCerrar.HasDelegate)
        {
            await AlCerrar.InvokeAsync();
        }
    }

    public void Dispose() => PararTemporizador();
}
