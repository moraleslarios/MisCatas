using System.Globalization;
using Microsoft.AspNetCore.Components;
using MisCatas.Web.Components.Shared.Iconos;

namespace MisCatas.Web.Components.Shared;

/// <summary>
/// Cuenta atrás visual (anillo de progreso + reloj). Solo presentación: el
/// tiempo lo aporta quien la usa. El motor en vivo llega en la Fase 3.
/// </summary>
public partial class CuentaAtras
{
    private const double Radio = 52d;

    /// <summary>Segundos que quedan.</summary>
    [Parameter]
    public int SegundosRestantes { get; set; }

    /// <summary>Segundos totales concedidos para votar.</summary>
    [Parameter]
    public int SegundosTotales { get; set; } = 90;

    /// <summary>Porcentaje restante por debajo del cual se entra en alerta.</summary>
    [Parameter]
    public int PorcentajeAlerta { get; set; } = 25;

    /// <summary>La votación está detenida por el administrador.</summary>
    [Parameter]
    public bool Pausada { get; set; }

    /// <summary>Tamaño según el dispositivo de destino.</summary>
    [Parameter]
    public TamanoCuentaAtras Tamano { get; set; } = TamanoCuentaAtras.Movil;

    /// <summary>Clases CSS extra.</summary>
    [Parameter]
    public string? Clase { get; set; }

    private bool EsEscaparate => Tamano == TamanoCuentaAtras.Escaparate;

    private TamanoIcono TamanoAviso => EsEscaparate ? TamanoIcono.Grande : TamanoIcono.Pequeno;

    private int RestantesLimpios => Math.Max(0, SegundosRestantes);

    private int TotalesLimpios => Math.Max(1, SegundosTotales);

    private bool SeAgoto => RestantesLimpios == 0;

    private double FraccionRestante =>
        Math.Clamp((double)RestantesLimpios / TotalesLimpios, 0d, 1d);

    private bool EnAlerta =>
        !Pausada && !SeAgoto && FraccionRestante * 100d <= PorcentajeAlerta;

    private static double Circunferencia => 2d * Math.PI * Radio;

    private double Desplazamiento => Circunferencia * (1d - FraccionRestante);

    private string TextoReloj
    {
        get
        {
            var minutos = RestantesLimpios / 60;
            var segundos = RestantesLimpios % 60;

            return minutos > 0
                ? $"{minutos}:{segundos:00}"
                : segundos.ToString(CultureInfo.InvariantCulture);
        }
    }

    private string TextoEstado
    {
        get
        {
            if (Pausada)
            {
                return "Votación en pausa";
            }

            if (SeAgoto)
            {
                return "Se acabó el tiempo";
            }

            return EnAlerta ? "¡Últimos segundos!" : "Tiempo para votar";
        }
    }

    /// <summary>
    /// Texto para lectores de pantalla. Se emite solo en múltiplos de diez
    /// segundos o en la cuenta final, para no interrumpir constantemente.
    /// </summary>
    private string TextoAnuncio
    {
        get
        {
            if (Pausada)
            {
                return "La votación está en pausa.";
            }

            if (SeAgoto)
            {
                return "Se ha acabado el tiempo para votar.";
            }

            if (RestantesLimpios > 5 && RestantesLimpios % 10 != 0)
            {
                return "";
            }

            return RestantesLimpios == 1
                ? "Queda 1 segundo."
                : $"Quedan {RestantesLimpios} segundos.";
        }
    }

    private string ClaseEstado
    {
        get
        {
            if (Pausada)
            {
                return "cuenta--pausada";
            }

            if (SeAgoto)
            {
                return "cuenta--agotada";
            }

            return EnAlerta ? "cuenta--alerta" : "cuenta--normal";
        }
    }

    private static string TextoInvariante(double valor) =>
        valor.ToString("0.###", CultureInfo.InvariantCulture);
}

/// <summary>Tamaños previstos para la cuenta atrás.</summary>
public enum TamanoCuentaAtras
{
    /// <summary>Móvil vertical, dentro de la pantalla de votación.</summary>
    Movil = 0,

    /// <summary>Televisión del escaparate, legible a varios metros.</summary>
    Escaparate = 1,
}
