using MisCatas.Application.Votacion;
using MisCatas.Domain.Enums;

namespace MisCatas.Tests;

/// <summary>
/// Pruebas de la máquina de estados de la cata. Son las reglas duras que el
/// servidor NO debe dejar saltarse por mucho que la interfaz insista.
/// </summary>
public class TransicionesCataTests
{
    [Theory]
    [InlineData(EstadoCata.Borrador, EstadoCata.Abierta)]
    [InlineData(EstadoCata.Abierta, EstadoCata.EnCurso)]
    [InlineData(EstadoCata.Abierta, EstadoCata.Borrador)]
    [InlineData(EstadoCata.Abierta, EstadoCata.Finalizada)]
    [InlineData(EstadoCata.EnCurso, EstadoCata.Finalizada)]
    [InlineData(EstadoCata.Finalizada, EstadoCata.EnCurso)]
    public void Transiciones_permitidas(EstadoCata origen, EstadoCata destino)
    {
        Assert.True(TransicionesCata.EsValida(origen, destino));
    }

    [Theory]
    [InlineData(EstadoCata.Borrador, EstadoCata.EnCurso)]
    [InlineData(EstadoCata.Borrador, EstadoCata.Finalizada)]
    [InlineData(EstadoCata.EnCurso, EstadoCata.Borrador)]
    [InlineData(EstadoCata.EnCurso, EstadoCata.Abierta)]
    [InlineData(EstadoCata.Finalizada, EstadoCata.Borrador)]
    [InlineData(EstadoCata.Finalizada, EstadoCata.Abierta)]
    public void Transiciones_prohibidas(EstadoCata origen, EstadoCata destino)
    {
        Assert.False(TransicionesCata.EsValida(origen, destino));
    }

    [Theory]
    [InlineData(EstadoCata.Borrador, EstadoCata.Borrador)]
    [InlineData(EstadoCata.Abierta, EstadoCata.Abierta)]
    [InlineData(EstadoCata.EnCurso, EstadoCata.EnCurso)]
    [InlineData(EstadoCata.Finalizada, EstadoCata.Finalizada)]
    public void Nadie_puede_transitar_a_su_propio_estado(EstadoCata estado, EstadoCata mismo)
    {
        Assert.False(TransicionesCata.EsValida(estado, mismo));
    }

    [Fact]
    public void Una_cata_finalizada_siempre_se_puede_reabrir()
    {
        // Requisito explícito: la cata nunca se cierra sola y el anfitrión
        // puede volver atrás si se equivocó.
        Assert.Contains(EstadoCata.EnCurso, TransicionesCata.Destinos(EstadoCata.Finalizada));
    }

    [Fact]
    public void Solo_el_borrador_admite_cambios_de_montaje()
    {
        Assert.True(TransicionesCata.AdmiteCambiosDeMontaje(EstadoCata.Borrador));
        Assert.False(TransicionesCata.AdmiteCambiosDeMontaje(EstadoCata.Abierta));
        Assert.False(TransicionesCata.AdmiteCambiosDeMontaje(EstadoCata.EnCurso));
        Assert.False(TransicionesCata.AdmiteCambiosDeMontaje(EstadoCata.Finalizada));
    }

    [Fact]
    public void Los_datos_solo_se_revelan_al_finalizar()
    {
        Assert.False(TransicionesCata.DatosRevelados(EstadoCata.Borrador));
        Assert.False(TransicionesCata.DatosRevelados(EstadoCata.Abierta));
        Assert.False(TransicionesCata.DatosRevelados(EstadoCata.EnCurso));
        Assert.True(TransicionesCata.DatosRevelados(EstadoCata.Finalizada));
    }

    [Fact]
    public void Cada_destino_tiene_un_texto_para_el_boton()
    {
        foreach (var origen in Enum.GetValues<EstadoCata>())
        {
            foreach (var destino in TransicionesCata.Destinos(origen))
            {
                Assert.False(string.IsNullOrWhiteSpace(TransicionesCata.TextoTransicion(destino)));
            }
        }
    }
}
