using MisCatas.Application.Votacion;

namespace MisCatas.Tests;

/// <summary>
/// Pruebas del cálculo de resultados. Es la pieza más delicada de la cata:
/// si la media o el desempate fallan, se corona al vino equivocado.
/// </summary>
public class CalculadoraResultadosTests
{
    private static NotasDeVino Vino(int orden, decimal[] notas, int[]? tiempos = null) =>
        new(orden, orden * 100, notas, tiempos ?? []);

    [Fact]
    public void Media_de_una_lista_simple_es_la_aritmetica()
    {
        var metricas = CalculadoraResultados.Calcular(Vino(1, [8m, 9m, 10m]));

        Assert.Equal(9m, metricas.Media);
        Assert.Equal(3, metricas.VotosContados);
        Assert.Equal(10m, metricas.MejorNota);
        Assert.Equal(8m, metricas.PeorNota);
    }

    [Fact]
    public void Media_se_redondea_a_dos_decimales()
    {
        // 8 + 9 + 9,5 = 26,5 → 8,8333… → 8,83
        var metricas = CalculadoraResultados.Calcular(Vino(1, [8m, 9m, 9.5m]));

        Assert.Equal(8.83m, metricas.Media);
    }

    [Fact]
    public void Vino_sin_notas_no_tiene_media()
    {
        var metricas = CalculadoraResultados.Calcular(Vino(1, []));

        Assert.Null(metricas.Media);
        Assert.Equal(0, metricas.VotosContados);
        Assert.Null(metricas.MejorNota);
        Assert.Null(metricas.PeorNota);
        Assert.Equal("—", metricas.MediaTexto);
    }

    [Fact]
    public void Una_sola_nota_tiene_desviacion_cero()
    {
        var metricas = CalculadoraResultados.Calcular(Vino(1, [7m]));

        Assert.Equal(7m, metricas.Media);
        Assert.Equal(0m, metricas.DesviacionTipica);
    }

    [Fact]
    public void Notas_identicas_tienen_desviacion_cero()
    {
        var metricas = CalculadoraResultados.Calcular(Vino(1, [6m, 6m, 6m, 6m]));

        Assert.Equal(0m, metricas.DesviacionTipica);
    }

    [Fact]
    public void Tiempo_medio_es_la_media_de_los_segundos()
    {
        var metricas = CalculadoraResultados.Calcular(Vino(1, [8m, 8m], [10, 20]));

        Assert.Equal(15d, metricas.TiempoMedioSegundos);
    }

    [Fact]
    public void Sin_tiempos_el_tiempo_medio_es_nulo()
    {
        var metricas = CalculadoraResultados.Calcular(Vino(1, [8m]));

        Assert.Null(metricas.TiempoMedioSegundos);
        Assert.Equal("—", metricas.TiempoMedioTexto);
    }

    [Fact]
    public void Clasificar_ordena_de_mayor_a_menor_media()
    {
        var clasificacion = CalculadoraResultados.Clasificar(
        [
            Vino(1, [5m]),
            Vino(2, [9m]),
            Vino(3, [7m]),
        ]);

        Assert.Equal([2, 3, 1], clasificacion.Select(v => v.NumeroOrden));
        Assert.Equal([1, 2, 3], clasificacion.Select(v => v.Posicion));
    }

    [Fact]
    public void Empate_a_media_lo_gana_la_menor_desviacion_tipica()
    {
        // Ambos promedian 7, pero el vino 2 tiene a la mesa de acuerdo.
        var clasificacion = CalculadoraResultados.Clasificar(
        [
            Vino(1, [4m, 10m]),
            Vino(2, [7m, 7m]),
        ]);

        Assert.Equal(2, clasificacion[0].NumeroOrden);
        Assert.Equal(1, clasificacion[1].NumeroOrden);
    }

    [Fact]
    public void Empate_total_lo_gana_el_que_tiene_mas_votos()
    {
        var clasificacion = CalculadoraResultados.Clasificar(
        [
            Vino(1, [8m]),
            Vino(2, [8m, 8m, 8m]),
        ]);

        Assert.Equal(2, clasificacion[0].NumeroOrden);
    }

    [Fact]
    public void Empate_absoluto_se_resuelve_por_numero_de_orden()
    {
        var clasificacion = CalculadoraResultados.Clasificar(
        [
            Vino(3, [8m, 8m]),
            Vino(1, [8m, 8m]),
            Vino(2, [8m, 8m]),
        ]);

        Assert.Equal([1, 2, 3], clasificacion.Select(v => v.NumeroOrden));
    }

    [Fact]
    public void Vinos_sin_media_quedan_al_final()
    {
        var clasificacion = CalculadoraResultados.Clasificar(
        [
            Vino(1, []),
            Vino(2, [1m]),
        ]);

        Assert.Equal(2, clasificacion[0].NumeroOrden);
        Assert.Equal(1, clasificacion[1].NumeroOrden);
    }

    [Fact]
    public void Clasificar_una_lista_vacia_no_revienta()
    {
        var clasificacion = CalculadoraResultados.Clasificar([]);

        Assert.Empty(clasificacion);
    }

    [Fact]
    public void MediaTexto_usa_un_decimal()
    {
        var metricas = CalculadoraResultados.Calcular(Vino(1, [8.5m]));

        // El separador decimal depende de la cultura activa; lo que nos importa
        // es que salga un único decimal y no la nota en crudo.
        Assert.Contains(metricas.MediaTexto, new[] { "8,5", "8.5" });
    }
}
