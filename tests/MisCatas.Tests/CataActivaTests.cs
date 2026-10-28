using MisCatas.Application.Catas;
using MisCatas.Domain.Enums;

namespace MisCatas.Tests;

/// <summary>
/// Pruebas de la regla dura «solo puede haber una cata activa a la vez».
/// </summary>
public class CataActivaTests
{
    [Theory]
    [InlineData(EstadoCata.Abierta)]
    [InlineData(EstadoCata.EnCurso)]
    public void Abierta_y_en_curso_ocupan_el_hueco(EstadoCata estado)
    {
        Assert.True(CataActiva.Es(estado));
        Assert.Contains(estado, CataActiva.Estados);
    }

    [Theory]
    [InlineData(EstadoCata.Borrador)]
    [InlineData(EstadoCata.Finalizada)]
    public void Borrador_y_finalizada_no_ocupan_el_hueco(EstadoCata estado)
    {
        Assert.False(CataActiva.Es(estado));
        Assert.DoesNotContain(estado, CataActiva.Estados);
    }

    [Fact]
    public void El_mensaje_distingue_abierta_de_en_curso()
    {
        var enCurso = CataActiva.MensajeOcupado("Cata 1", EstadoCata.EnCurso);
        var abierta = CataActiva.MensajeOcupado("Cata 1", EstadoCata.Abierta);

        Assert.NotEqual(enCurso, abierta);
        Assert.Contains("en curso", enCurso);
        Assert.Contains("borrador", abierta);
    }

    [Fact]
    public void El_mensaje_nombra_la_cata_que_bloquea()
    {
        var mensaje = CataActiva.MensajeOcupado("Cata de Navidad", EstadoCata.Abierta);

        Assert.Contains("«Cata de Navidad»", mensaje);
    }

    [Fact]
    public void El_filtro_se_puede_compilar_y_deja_pasar_solo_las_activas()
    {
        var filtro = CataActiva.Filtro.Compile();

        Assert.True(filtro(new() { Estado = EstadoCata.Abierta }));
        Assert.True(filtro(new() { Estado = EstadoCata.EnCurso }));
        Assert.False(filtro(new() { Estado = EstadoCata.Borrador }));
        Assert.False(filtro(new() { Estado = EstadoCata.Finalizada }));
    }
}
