using MisCatas.Application.Votacion;
using MisCatas.Domain.Enums;

namespace MisCatas.Tests;

/// <summary>
/// Pruebas de la proyección <c>CataViva.AEstado</c>, que es el punto donde el
/// reloj autoritativo del servidor se convierte en la cifra que ven los diez
/// móviles y las dos televisiones.
///
/// El caso delicado es la PAUSA: se guarda el REMANENTE de segundos, no el
/// instante de fin, de modo que la votación pueda quedar congelada el tiempo
/// que haga falta (y sobrevivir a un reinicio) sin regalar ni robar segundos.
/// </summary>
public class CataVivaTests
{
    private static readonly DateTime Ahora = new(2026, 10, 19, 20, 0, 0, DateTimeKind.Utc);

    private static CataViva Viva(
        FaseVotacion fase,
        DateTime? finPrevistoUtc = null,
        DateTime? aperturaPrevistaUtc = null,
        int? segundosPausaRestantes = null,
        int segundosTotales = 90) =>
        new(
            CataId: 1,
            EstadoCata: EstadoCata.EnCurso,
            TotalVinos: 4,
            SegundosTotales: segundosTotales,
            PorcentajeAlertaRoja: 25,
            NumeroVinoActual: 2,
            Fase: fase,
            AperturaPrevistaUtc: aperturaPrevistaUtc,
            FinPrevistoUtc: finPrevistoUtc,
            AlertaEnviada: false,
            RevelacionActiva: false,
            AdminControlaId: 7,
            SegundosPausaRestantes: segundosPausaRestantes);

    [Fact]
    public void Con_la_votacion_abierta_el_reloj_sale_del_fin_previsto()
    {
        var estado = Viva(FaseVotacion.Abierta, finPrevistoUtc: Ahora.AddSeconds(34)).AEstado(Ahora);

        Assert.Equal(FaseVotacion.Abierta, estado.Fase);
        Assert.Equal(34d, estado.SegundosRestantes, precision: 3);
        Assert.True(estado.AceptaVotos);
    }

    [Fact]
    public void Una_votacion_abierta_pasada_de_hora_no_da_segundos_negativos()
    {
        var estado = Viva(FaseVotacion.Abierta, finPrevistoUtc: Ahora.AddSeconds(-12)).AEstado(Ahora);

        Assert.Equal(0d, estado.SegundosRestantes);
        Assert.False(estado.AceptaVotos);
    }

    [Fact]
    public void En_pausa_se_envia_el_remanente_congelado()
    {
        // El fin previsto es basura mientras está pausada (quedó en el pasado);
        // lo que manda es el remanente guardado.
        var estado = Viva(
            FaseVotacion.Pausada,
            finPrevistoUtc: Ahora.AddSeconds(-600),
            segundosPausaRestantes: 41).AEstado(Ahora);

        Assert.Equal(FaseVotacion.Pausada, estado.Fase);
        Assert.Equal(41d, estado.SegundosRestantes);
        Assert.True(estado.EstaPausada);
        Assert.False(estado.AceptaVotos);
    }

    [Fact]
    public void La_pausa_no_se_descongela_con_el_paso_del_tiempo()
    {
        // Dos lecturas separadas diez minutos deben dar la MISMA cifra: es lo
        // que garantiza que la pausa pueda durar indefinidamente.
        var viva = Viva(
            FaseVotacion.Pausada,
            finPrevistoUtc: Ahora.AddSeconds(41),
            segundosPausaRestantes: 41);

        Assert.Equal(
            viva.AEstado(Ahora).SegundosRestantes,
            viva.AEstado(Ahora.AddMinutes(10)).SegundosRestantes);
    }

    [Fact]
    public void Una_pausa_sin_remanente_no_revienta()
    {
        // Defensa ante un estado incoherente: mejor cero que una excepción en
        // mitad de una cata.
        var estado = Viva(FaseVotacion.Pausada, segundosPausaRestantes: null).AEstado(Ahora);

        Assert.Equal(0d, estado.SegundosRestantes);
        Assert.True(estado.EstaPausada);
    }

    [Fact]
    public void En_preparacion_el_reloj_de_votacion_esta_a_cero_y_el_de_cuenta_atras_corre()
    {
        // Son dos tiempos distintos y viajan en campos distintos: confundirlos
        // es lo que hacía que en los móviles solo se viera un «0» parpadeando.
        var estado = Viva(
            FaseVotacion.Preparacion,
            aperturaPrevistaUtc: Ahora.AddSeconds(3.2)).AEstado(Ahora);

        Assert.Equal(0d, estado.SegundosRestantes);
        Assert.Equal(4, estado.SegundosPreparacion);
    }

    [Fact]
    public void Fuera_de_la_preparacion_no_hay_cuenta_atras_previa()
    {
        var estado = Viva(
            FaseVotacion.Abierta,
            finPrevistoUtc: Ahora.AddSeconds(50),
            aperturaPrevistaUtc: Ahora.AddSeconds(3)).AEstado(Ahora);

        Assert.Equal(0, estado.SegundosPreparacion);
    }

    [Theory]
    [InlineData(FaseVotacion.Inactiva)]
    [InlineData(FaseVotacion.Cerrada)]
    public void Sin_votacion_viva_el_reloj_va_a_cero(FaseVotacion fase)
    {
        var estado = Viva(fase, finPrevistoUtc: Ahora.AddSeconds(50)).AEstado(Ahora);

        Assert.Equal(0d, estado.SegundosRestantes);
    }

    [Fact]
    public void El_reposo_arranca_inactivo_y_sin_pausa()
    {
        var reposo = CataViva.EnReposo(
            cataId: 1,
            estadoCata: EstadoCata.Abierta,
            totalVinos: 4,
            segundosTotales: 90,
            porcentajeAlertaRoja: 25,
            revelacionActiva: false,
            adminControlaId: null);

        Assert.Equal(FaseVotacion.Inactiva, reposo.Fase);
        Assert.Null(reposo.SegundosPausaRestantes);
        Assert.Null(reposo.NumeroVinoActual);
        Assert.False(reposo.AEstado(Ahora).EstaPausada);
    }

    [Fact]
    public void La_proyeccion_arrastra_los_datos_de_cabecera()
    {
        var estado = Viva(FaseVotacion.Abierta, finPrevistoUtc: Ahora.AddSeconds(10)).AEstado(Ahora);

        Assert.Equal(1, estado.CataId);
        Assert.Equal(2, estado.NumeroVinoActual);
        Assert.Equal(4, estado.TotalVinos);
        Assert.Equal(90, estado.SegundosTotales);
        Assert.Equal(7, estado.AdminControlaId);
    }
}
