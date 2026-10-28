namespace MisCatas.Application.Votacion;

/// <summary>
/// Calculadora de resultados de una cata. Es deliberadamente pura (sin base de
/// datos y sin dependencias) para poder probar las reglas del concurso sin
/// montar un escenario entero:
///
/// <list type="bullet">
///   <item>la nota de un vino es la MEDIA ARITMÉTICA de sus notas válidas;</item>
///   <item>los no-votos con política <c>NoCuenta</c> se excluyen antes de llegar aquí;</item>
///   <item>en caso de EMPATE gana el vino con MENOR DESVIACIÓN TÍPICA, es decir,
///         el que ha generado más consenso en la mesa.</item>
/// </list>
/// </summary>
public static class CalculadoraResultados
{
    /// <summary>
    /// Ordena los vinos y calcula sus métricas. La posición 1 es el ganador.
    /// </summary>
    public static IReadOnlyList<MetricasVino> Clasificar(IEnumerable<NotasDeVino> vinos)
    {
        ArgumentNullException.ThrowIfNull(vinos);

        var calculados = vinos.Select(Calcular).ToList();

        // Primero la media más alta. Si empatan, gana el de menor desviación
        // típica (más consenso). Si también empatan, manda el número de votos
        // y, por último, el orden de servicio para que el resultado sea estable.
        var ordenados = calculados
            .OrderByDescending(v => v.Media ?? decimal.MinValue)
            .ThenBy(v => v.DesviacionTipica ?? decimal.MaxValue)
            .ThenByDescending(v => v.VotosContados)
            .ThenBy(v => v.NumeroOrden)
            .ToList();

        return [.. ordenados.Select((v, indice) => v with { Posicion = indice + 1 })];
    }

    /// <summary>Métricas de un solo vino, sin posición asignada todavía.</summary>
    public static MetricasVino Calcular(NotasDeVino vino)
    {
        ArgumentNullException.ThrowIfNull(vino);

        var notas = vino.Notas ?? [];
        var tiempos = vino.SegundosEnVotar ?? [];

        if (notas.Count == 0)
        {
            return new MetricasVino(
                Posicion: 0,
                NumeroOrden: vino.NumeroOrden,
                VinoId: vino.VinoId,
                Media: null,
                VotosContados: 0,
                MejorNota: null,
                PeorNota: null,
                DesviacionTipica: null,
                TiempoMedioSegundos: tiempos.Count == 0 ? null : tiempos.Average());
        }

        var media = notas.Average();

        return new MetricasVino(
            Posicion: 0,
            NumeroOrden: vino.NumeroOrden,
            VinoId: vino.VinoId,
            Media: Redondear(media),
            VotosContados: notas.Count,
            MejorNota: notas.Max(),
            PeorNota: notas.Min(),
            DesviacionTipica: DesviacionTipica(notas, media),
            TiempoMedioSegundos: tiempos.Count == 0 ? null : tiempos.Average());
    }

    /// <summary>
    /// Media aritmética redondeada a dos decimales, o <c>null</c> si no hay notas.
    /// </summary>
    public static decimal? Media(IReadOnlyList<decimal> notas) =>
        notas is null || notas.Count == 0 ? null : Redondear(notas.Average());

    /// <summary>
    /// Desviación típica POBLACIONAL de las notas, redondeada a cuatro
    /// decimales. Con una sola nota es 0: un único votante es consenso pleno.
    /// </summary>
    public static decimal? DesviacionTipica(IReadOnlyList<decimal> notas)
    {
        if (notas is null || notas.Count == 0)
        {
            return null;
        }

        return DesviacionTipica(notas, notas.Average());
    }

    private static decimal DesviacionTipica(IReadOnlyList<decimal> notas, decimal media)
    {
        if (notas.Count == 1)
        {
            return 0m;
        }

        var varianza = notas.Sum(n => (n - media) * (n - media)) / notas.Count;

        return Math.Round((decimal)Math.Sqrt((double)varianza), 4, MidpointRounding.AwayFromZero);
    }

    private static decimal Redondear(decimal valor) =>
        Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
