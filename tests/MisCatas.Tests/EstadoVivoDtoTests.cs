using MisCatas.Application.Votacion;
using MisCatas.Domain.Enums;

namespace MisCatas.Tests;

/// <summary>
/// Pruebas del DTO que gobierna el reloj y la fase en todas las pantallas.
/// Si estas propiedades fallan, los diez móviles dejan de ir al unísono.
/// </summary>
public class EstadoVivoDtoTests
{
    private static EstadoVivoDto Estado(
        FaseVotacion fase,
        double segundosRestantes,
        int segundosTotales = 90,
        int porcentajeAlerta = 25,
        EstadoCata estadoCata = EstadoCata.EnCurso) =>
        new(
            CataId: 1,
            EstadoCata: estadoCata,
            Fase: fase,
            NumeroVinoActual: 1,
            TotalVinos: 4,
            SegundosTotales: segundosTotales,
            PorcentajeAlertaRoja: porcentajeAlerta,
            FinPrevistoUtc: DateTime.UtcNow.AddSeconds(segundosRestantes),
            SegundosRestantes: segundosRestantes,
            SegundosPreparacion: 5,
            RevelacionActiva: false,
            AdminControlaId: null);

    [Fact]
    public void Los_segundos_enteros_redondean_hacia_arriba()
    {
        Assert.Equal(13, Estado(FaseVotacion.Abierta, 12.1d).SegundosRestantesEnteros);
        Assert.Equal(12, Estado(FaseVotacion.Abierta, 12d).SegundosRestantesEnteros);
    }

    [Fact]
    public void Los_segundos_enteros_nunca_son_negativos()
    {
        Assert.Equal(0, Estado(FaseVotacion.Cerrada, -8d).SegundosRestantesEnteros);
    }

    [Fact]
    public void El_umbral_de_alerta_es_el_porcentaje_del_total()
    {
        Assert.Equal(22.5d, Estado(FaseVotacion.Abierta, 90d).UmbralAlertaSegundos);
    }

    [Fact]
    public void No_hay_alerta_roja_mientras_sobra_tiempo()
    {
        Assert.False(Estado(FaseVotacion.Abierta, 40d).EnAlertaRoja);
    }

    [Fact]
    public void Hay_alerta_roja_al_bajar_del_umbral()
    {
        Assert.True(Estado(FaseVotacion.Abierta, 22d).EnAlertaRoja);
        Assert.True(Estado(FaseVotacion.Abierta, 22.5d).EnAlertaRoja);
    }

    [Theory]
    [InlineData(FaseVotacion.Inactiva)]
    [InlineData(FaseVotacion.Preparacion)]
    [InlineData(FaseVotacion.Cerrada)]
    public void Sin_votacion_abierta_no_hay_alerta_roja(FaseVotacion fase)
    {
        Assert.False(Estado(fase, 1d).EnAlertaRoja);
    }

    [Fact]
    public void Solo_se_aceptan_votos_con_la_votacion_abierta_y_tiempo()
    {
        Assert.True(Estado(FaseVotacion.Abierta, 5d).AceptaVotos);
        Assert.False(Estado(FaseVotacion.Abierta, 0d).AceptaVotos);
        Assert.False(Estado(FaseVotacion.Preparacion, 5d).AceptaVotos);
        Assert.False(Estado(FaseVotacion.Cerrada, 5d).AceptaVotos);
        Assert.False(Estado(FaseVotacion.Inactiva, 5d).AceptaVotos);
    }

    [Fact]
    public void Solo_se_rectifica_entre_votaciones_y_con_la_cata_en_curso()
    {
        Assert.True(Estado(FaseVotacion.Cerrada, 0d).PermiteRectificar);
        Assert.True(Estado(FaseVotacion.Inactiva, 0d).PermiteRectificar);
        Assert.False(Estado(FaseVotacion.Abierta, 30d).PermiteRectificar);
        Assert.False(Estado(FaseVotacion.Preparacion, 3d).PermiteRectificar);
        Assert.False(Estado(FaseVotacion.Cerrada, 0d, estadoCata: EstadoCata.Finalizada).PermiteRectificar);
    }

    [Fact]
    public void El_estado_en_reposo_no_acepta_votos_ni_tiene_reloj()
    {
        var reposo = EstadoVivoDto.EnReposo(1, EstadoCata.Abierta, totalVinos: 4,
            segundosTotales: 90, porcentajeAlertaRoja: 25);

        Assert.Equal(FaseVotacion.Inactiva, reposo.Fase);
        Assert.False(reposo.AceptaVotos);
        Assert.False(reposo.EnAlertaRoja);
        Assert.Equal(0, reposo.SegundosRestantesEnteros);
        Assert.Null(reposo.NumeroVinoActual);
    }

    [Fact]
    public void Un_total_de_cero_segundos_no_dispara_la_alerta()
    {
        // Defensa contra una configuración a cero: no queremos divisiones raras
        // ni una pantalla roja permanente.
        Assert.False(Estado(FaseVotacion.Abierta, 0d, segundosTotales: 0).EnAlertaRoja);
    }

    // --------------------------------------------------------------- pausa

    [Fact]
    public void La_pausa_se_delata_solo_en_su_fase()
    {
        Assert.True(Estado(FaseVotacion.Pausada, 30d).EstaPausada);
        Assert.False(Estado(FaseVotacion.Abierta, 30d).EstaPausada);
        Assert.False(Estado(FaseVotacion.Cerrada, 0d).EstaPausada);
    }

    [Fact]
    public void En_pausa_no_se_aceptan_votos_aunque_quede_tiempo()
    {
        // Es la defensa clave del requisito: el velo visual es cosmético, lo que
        // de verdad bloquea el voto es esta propiedad.
        Assert.False(Estado(FaseVotacion.Pausada, 45d).AceptaVotos);
    }

    [Fact]
    public void En_pausa_no_se_enciende_la_alerta_roja()
    {
        // Con el reloj congelado en 3 s no tiene sentido parpadear en rojo:
        // nadie está perdiendo tiempo.
        Assert.False(Estado(FaseVotacion.Pausada, 3d).EnAlertaRoja);
    }

    [Fact]
    public void En_pausa_no_se_permite_rectificar()
    {
        // La votación sigue abierta, solo congelada: la ventana de
        // rectificación llega después de cerrarla.
        Assert.False(Estado(FaseVotacion.Pausada, 45d).PermiteRectificar);
    }

    [Fact]
    public void En_pausa_se_conserva_el_remanente_para_rotularlo()
    {
        // La cifra congelada se sigue pintando: el usuario debe ver cuánto
        // tiempo le quedará cuando el administrador reanude.
        Assert.Equal(45, Estado(FaseVotacion.Pausada, 44.2d).SegundosRestantesEnteros);
    }
}
