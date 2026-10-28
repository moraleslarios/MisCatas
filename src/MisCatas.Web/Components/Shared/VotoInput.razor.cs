using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Entrada de nota de cata. Combina deslizador, botones de paso y campo de
/// texto con coma decimal. Admite <c>@bind-Valor</c>.
/// </summary>
public partial class VotoInput
{
    /// <summary>Nota máxima absoluta del sistema. Ninguna votación puede pasar de aquí.</summary>
    public const decimal NotaMaxima = 10m;

    /// <summary>Nota mínima absoluta del sistema.</summary>
    public const decimal NotaMinima = 0m;

    private static readonly CultureInfo Espanol = CultureInfo.GetCultureInfo("es-ES");

    private readonly string _id = Guid.NewGuid().ToString("N")[..8];

    private string _textoCampo = "";
    private string? _error;
    private decimal? _valorPintado;

    /// <summary>Nota elegida. <c>null</c> mientras no se ha tocado nada.</summary>
    [Parameter]
    public decimal? Valor { get; set; }

    /// <summary>Notifica el cambio de <see cref="Valor"/>.</summary>
    [Parameter]
    public EventCallback<decimal?> ValorChanged { get; set; }

    /// <summary>Nota mínima.</summary>
    [Parameter]
    public decimal Minimo { get; set; } = NotaMinima;

    /// <summary>Nota máxima. Se recorta a <see cref="NotaMaxima"/>.</summary>
    [Parameter]
    public decimal Maximo { get; set; } = NotaMaxima;

    /// <summary>Incremento permitido. Viene de la configuración de la cata.</summary>
    [Parameter]
    public decimal Paso { get; set; } = 0.5m;

    /// <summary>Bloquea la entrada (votación cerrada, por ejemplo).</summary>
    [Parameter]
    public bool Deshabilitado { get; set; }

    /// <summary>Etiqueta para lectores de pantalla.</summary>
    [Parameter]
    public string EtiquetaAccesible { get; set; } = "Nota del vino";

    /// <summary>Clases CSS extra.</summary>
    [Parameter]
    public string? Clase { get; set; }

    /// <summary>Valor que usan el deslizador y los botones cuando aún no hay nota.</summary>
    private decimal ValorEfectivo => Valor ?? PuntoMedio;

    private decimal PuntoMedio => Redondear((Minimo + Maximo) / 2m);

    private bool PuedeBajar => ValorEfectivo > Minimo;

    private bool PuedeSubir => ValorEfectivo < Maximo;

    private string TextoCifra => Valor is decimal nota ? TextoNota(nota) : "—";

    private string TextoPaso => TextoNota(Paso);

    protected override void OnParametersSet()
    {
        if (Paso <= 0m)
        {
            Paso = 0.5m;
        }

        // Tope DURO: aunque quien use el componente pase otro rango, ninguna
        // nota puede salirse de 0,0–10,0. Esta es la última barrera del cliente;
        // el servidor vuelve a validarlo en ServicioVotacion.ValidarNota.
        if (Minimo < NotaMinima)
        {
            Minimo = NotaMinima;
        }

        if (Maximo > NotaMaxima)
        {
            Maximo = NotaMaxima;
        }

        if (Maximo <= Minimo)
        {
            Minimo = NotaMinima;
            Maximo = NotaMaxima;
        }

        // El paso nunca puede ser mayor que el propio rango.
        if (Paso > Maximo - Minimo)
        {
            Paso = 0.5m;
        }

        // Si el valor recibido se sale del rango se recorta en el acto.
        if (Valor is decimal recibido && (recibido < Minimo || recibido > Maximo))
        {
            Valor = Math.Clamp(recibido, Minimo, Maximo);
        }

        // Solo se reescribe el campo cuando el valor cambia desde fuera: así no
        // se le borra al usuario lo que está escribiendo.
        if (_valorPintado != Valor)
        {
            _valorPintado = Valor;
            _textoCampo = Valor is decimal nota ? TextoNota(nota) : "";
            _error = null;
        }
    }

    private int Decimales
    {
        get
        {
            var paso = Paso;
            var decimales = 0;

            while (paso != Math.Truncate(paso) && decimales < 2)
            {
                paso *= 10m;
                decimales++;
            }

            return decimales;
        }
    }

    private string TextoNota(decimal valor) =>
        valor.ToString($"F{Decimales}", Espanol);

    private static string TextoInvariante(decimal valor) =>
        valor.ToString(CultureInfo.InvariantCulture);

    /// <summary>Ajusta al paso y recorta al rango permitido.</summary>
    private decimal Redondear(decimal valor)
    {
        var pasos = Math.Round(valor / Paso, MidpointRounding.AwayFromZero);
        var ajustado = pasos * Paso;

        if (ajustado < Minimo)
        {
            ajustado = Minimo;
        }

        if (ajustado > Maximo)
        {
            ajustado = Maximo;
        }

        return decimal.Round(ajustado, Decimales);
    }

    private async Task EstablecerAsync(decimal valor)
    {
        var limpio = Redondear(valor);

        _error = null;
        _textoCampo = TextoNota(limpio);
        _valorPintado = limpio;
        Valor = limpio;

        if (ValorChanged.HasDelegate)
        {
            await ValorChanged.InvokeAsync(limpio);
        }
    }

    private Task BajarAsync() => EstablecerAsync(ValorEfectivo - Paso);

    private Task SubirAsync() => EstablecerAsync(ValorEfectivo + Paso);

    private async Task AlDeslizarAsync(ChangeEventArgs argumentos)
    {
        var texto = argumentos.Value?.ToString();

        // El deslizador siempre manda punto decimal (HTML usa cultura invariante).
        if (decimal.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
        {
            await EstablecerAsync(valor);
        }
    }

    private async Task AlEscribirAsync(ChangeEventArgs argumentos)
    {
        var texto = argumentos.Value?.ToString()?.Trim() ?? "";

        _textoCampo = texto;

        if (texto.Length == 0)
        {
            _error = null;
            _valorPintado = null;
            Valor = null;

            if (ValorChanged.HasDelegate)
            {
                await ValorChanged.InvokeAsync(null);
            }

            return;
        }

        // Se acepta tanto coma como punto: la gente escribe lo que le sale.
        var normalizado = texto.Replace(',', '.');

        if (!decimal.TryParse(normalizado, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor))
        {
            _error = "Escribe un número, por ejemplo 7,5.";
            return;
        }

        if (valor < Minimo || valor > Maximo)
        {
            _error = $"La nota tiene que estar entre {TextoNota(Minimo)} y {TextoNota(Maximo)}.";
            return;
        }

        var ajustado = Redondear(valor);

        _error = ajustado != decimal.Round(valor, Decimales)
            ? $"Se ha ajustado a {TextoNota(ajustado)}: el paso es de {TextoPaso}."
            : null;

        _textoCampo = TextoNota(ajustado);
        _valorPintado = ajustado;
        Valor = ajustado;

        if (ValorChanged.HasDelegate)
        {
            await ValorChanged.InvokeAsync(ajustado);
        }
    }
}
