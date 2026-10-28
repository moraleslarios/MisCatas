using MisCatas.Application.Participacion;
using MisCatas.Domain.Enums;

namespace MisCatas.Tests;

/// <summary>
/// Pruebas de los DTOs que viajan a las pantallas. Aquí vive la regla dura de
/// ocultación: mientras la cata no está finalizada el participante solo debe
/// poder leer «Vino nº N».
/// </summary>
public class ParticipacionDtosTests
{
    // --- Vino a ciegas ----------------------------------------------------

    [Fact]
    public void Un_vino_ciego_solo_se_presenta_por_su_numero()
    {
        var vino = new VinoCiegoDto(CataVinoId: 10, NumeroOrden: 3,
            EstadoVotacion: EstadoVotacion.Abierta, HeVotado: false,
            MiNota: null, MiComentario: null);

        Assert.Equal("Vino nº 3", vino.Titulo);
    }

    // --- Progreso ---------------------------------------------------------

    private static ProgresoCataDto Progreso(
        int total = 10, int hanVotado = 4, int totalVinos = 5, int vinosCerrados = 2,
        double? tiempoMedio = null) =>
        new(total, hanVotado, total - hanVotado, tiempoMedio, totalVinos, vinosCerrados, null, null);

    [Fact]
    public void El_porcentaje_votado_es_sobre_los_participantes()
    {
        Assert.Equal(40, Progreso(total: 10, hanVotado: 4).PorcentajeVotado);
    }

    [Fact]
    public void El_porcentaje_de_cata_es_sobre_los_vinos_cerrados()
    {
        Assert.Equal(40, Progreso(totalVinos: 5, vinosCerrados: 2).PorcentajeCata);
    }

    [Fact]
    public void Sin_participantes_el_porcentaje_votado_no_divide_por_cero()
    {
        Assert.Equal(0, Progreso(total: 0, hanVotado: 0).PorcentajeVotado);
    }

    [Fact]
    public void Sin_vinos_el_porcentaje_de_cata_no_divide_por_cero()
    {
        Assert.Equal(0, Progreso(totalVinos: 0, vinosCerrados: 0).PorcentajeCata);
    }

    [Fact]
    public void Sin_tiempo_medio_se_muestra_una_raya()
    {
        Assert.Equal("—", Progreso(tiempoMedio: null).TiempoMedioTexto);
    }

    [Fact]
    public void Con_tiempo_medio_se_muestra_el_valor()
    {
        Assert.NotEqual("—", Progreso(tiempoMedio: 12.5d).TiempoMedioTexto);
    }

    // --- Mis catas --------------------------------------------------------

    [Fact]
    public void MisCatas_vacio_no_tiene_nada()
    {
        Assert.True(MisCatasDto.Vacio.NoHayNada);
        Assert.Empty(MisCatasDto.Vacio.EnCurso);
        Assert.Empty(MisCatasDto.Vacio.Proximas);
        Assert.Empty(MisCatasDto.Vacio.Historico);
    }

    [Fact]
    public void MisCatas_con_una_proxima_ya_tiene_algo()
    {
        var cata = new CataUsuarioDto(1, "Cata 1", null, DateTime.Today,
            EstadoCata.Abierta, NumeroVinos: 4, NumeroParticipantes: 6, MisVotos: 0);

        var datos = new MisCatasDto([], [cata], []);

        Assert.False(datos.NoHayNada);
    }

    [Theory]
    [InlineData(EstadoCata.EnCurso, true, false)]
    [InlineData(EstadoCata.Finalizada, false, true)]
    [InlineData(EstadoCata.Abierta, false, false)]
    [InlineData(EstadoCata.Borrador, false, false)]
    public void Los_atajos_de_estado_de_una_cata_de_usuario(EstadoCata estado, bool enCurso, bool finalizada)
    {
        var cata = new CataUsuarioDto(1, "Cata", null, DateTime.Today, estado, 4, 6, 0);

        Assert.Equal(enCurso, cata.EstaEnCurso);
        Assert.Equal(finalizada, cata.EstaFinalizada);
    }

    // --- Voto revelado ----------------------------------------------------

    private static VotoReveladoDto Voto(decimal? nota, string? comentario = null,
        int segundos = 12, bool automatico = false) =>
        new(UsuarioId: 1, NombreUsuario: "Ana", Nota: nota, Comentario: comentario,
            SegundosEnVotar: segundos, FueRectificado: false, EsAutomatico: automatico);

    [Fact]
    public void Un_voto_automatico_es_un_no_voto()
    {
        var voto = Voto(null, automatico: true);

        Assert.True(voto.NoVoto);
        Assert.Equal("—", voto.NotaTexto);
        Assert.Equal("—", voto.TiempoTexto);
    }

    [Fact]
    public void Un_voto_normal_muestra_nota_con_un_decimal_y_su_tiempo()
    {
        var voto = Voto(7.5m, segundos: 20);

        Assert.False(voto.NoVoto);
        Assert.Equal("7,5", voto.NotaTexto);
        Assert.Equal("20 s", voto.TiempoTexto);
    }

    [Fact]
    public void Un_comentario_en_blanco_no_cuenta_como_comentario()
    {
        Assert.False(Voto(8m, comentario: null).TieneComentario);
        Assert.False(Voto(8m, comentario: "   ").TieneComentario);
        Assert.True(Voto(8m, comentario: "Elegante").TieneComentario);
    }

    // --- Vino revelado ----------------------------------------------------

    private static VinoReveladoDto Revelado(int posicion, params VotoReveladoDto[] votos) =>
        new(Posicion: posicion, NumeroOrden: 1, VinoId: 100, Nombre: "Rioja Alta",
            Anio: 2019, Clase: "Tinto", Bodega: "La Rioja Alta",
            DenominacionOrigen: "Rioja", Dueno: "Ana",
            PrecioEstimado: 20m, Comentarios: null, TieneFoto: false, TieneEtiqueta: false,
            NotaMedia: 8m, VotosContados: votos.Length, MiNota: null, MiComentario: null,
            Votos: votos);

    [Theory]
    [InlineData(1, "Oro")]
    [InlineData(2, "Plata")]
    [InlineData(3, "Bronce")]
    public void Los_tres_primeros_llevan_medalla(int posicion, string medalla)
    {
        Assert.Equal(medalla, Revelado(posicion).Medalla);
    }

    [Fact]
    public void A_partir_del_cuarto_no_hay_medalla()
    {
        Assert.Null(Revelado(4).Medalla);
    }

    [Fact]
    public void El_nombre_incluye_el_anio_cuando_lo_hay()
    {
        Assert.Equal("Rioja Alta (2019)", Revelado(1).NombreConAnio);
    }

    [Fact]
    public void Mejor_y_peor_nota_ignoran_los_no_votos()
    {
        var vino = Revelado(1,
            Voto(6m),
            Voto(9.5m),
            Voto(null, automatico: true));

        Assert.Equal(9.5m, vino.MejorNota);
        Assert.Equal(6m, vino.PeorNota);
    }

    [Fact]
    public void Se_cuentan_los_comentaristas()
    {
        var vino = Revelado(1,
            Voto(6m, "Áspero"),
            Voto(9m),
            Voto(8m, "Redondo"));

        Assert.Equal(2, vino.Comentaristas);
    }

    [Fact]
    public void El_ganador_es_el_primero_de_la_clasificacion()
    {
        var resultado = new ResultadoCataDto(1, "Cata", null, DateTime.Today, null,
            NumeroParticipantes: 6, Clasificacion: [Revelado(1), Revelado(2)]);

        Assert.NotNull(resultado.Ganador);
        Assert.Equal(1, resultado.Ganador!.Posicion);
    }

    [Fact]
    public void Una_clasificacion_vacia_no_tiene_ganador()
    {
        var resultado = new ResultadoCataDto(1, "Cata", null, DateTime.Today, null, 6, []);

        Assert.Null(resultado.Ganador);
    }
}
