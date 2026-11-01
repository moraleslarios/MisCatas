using Microsoft.Extensions.Options;
using MisCatas.Application.Comun;

namespace MisCatas.Tests;

/// <summary>
/// Pruebas de la dirección que se reparte a los invitados: la que viaja en el
/// código QR y en el mensaje de WhatsApp.
///
/// Lo que vigilan estas pruebas, y el motivo de que existan: la dirección tiene
/// que salir con el MISMO esquema y el MISMO puerto por los que está entrando
/// el organizador. Durante un tiempo la web obligaba a cifrar, y como el
/// certificado de la red local lo firmamos nosotros, el iPhone se negaba a
/// entrar sin dar ni la opción de confiar. Al servir la web por el puerto sin
/// cifrar, los QR tienen que seguirle: si alguien vuelve a forzar «https» aquí,
/// los invitados con iPhone se quedan fuera otra vez y nadie sabrá por qué.
/// </summary>
public class DireccionSitioTests
{
    private static ServicioDireccionSitio Servicio(
        string? urlPublica = null,
        string? urlEnEsteEquipo = null) =>
        new(Options.Create(new OpcionesSitio
        {
            UrlPublica = urlPublica,
            UrlEnEsteEquipo = urlEnEsteEquipo,
        }));

    [Theory]
    [InlineData("http://192.168.2.139:5081/", "http://192.168.2.139:5081")]
    [InlineData("https://192.168.2.139:5443/admin", "https://192.168.2.139:5443")]
    public void La_direccion_conserva_el_esquema_y_el_puerto_del_navegador(
        string navegador,
        string esperada)
    {
        var direccion = Servicio().Resolver(navegador);

        Assert.Equal(esperada, direccion.Raiz);
        Assert.Equal(OrigenDireccionSitio.Navegador, direccion.Origen);
        Assert.True(direccion.EsUtil);
    }

    [Fact]
    public void Entrar_sin_cifrar_no_convierte_la_direccion_en_https()
    {
        var direccion = Servicio().Resolver("http://192.168.2.139:5081/");

        Assert.StartsWith("http://", direccion.Raiz);
        Assert.DoesNotContain("https", direccion.Raiz);
    }

    [Fact]
    public void El_puerto_de_siempre_no_se_escribe_en_la_direccion()
    {
        // El 80 y el 443 son los que el navegador asume, y escribirlos deja una
        // dirección más larga de teclear sin ganar nada.
        Assert.Equal("http://miscatas.ejemplo.com", Servicio().Resolver("http://miscatas.ejemplo.com/").Raiz);
        Assert.Equal("https://miscatas.ejemplo.com", Servicio().Resolver("https://miscatas.ejemplo.com/").Raiz);
    }

    [Fact]
    public void Lo_escrito_a_mano_manda_sobre_lo_que_use_el_navegador()
    {
        // Es el caso de un dominio público o un puerto redirigido: cosas que la
        // aplicación no tiene forma de deducir mirando la petición.
        var direccion = Servicio(urlPublica: "https://catas.ejemplo.com/")
            .Resolver("http://localhost:5081/");

        Assert.Equal("https://catas.ejemplo.com", direccion.Raiz);
        Assert.Equal(OrigenDireccionSitio.Configurada, direccion.Origen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("esto-no-es-una-direccion")]
    [InlineData("/cuenta/entrar")]
    [InlineData("ftp://192.168.2.139")]
    public void Una_direccion_que_no_sirve_se_descarta_en_lugar_de_repartirse(string? navegador)
    {
        // Sin dirección de fiar se avisa en pantalla. Repartir un QR roto sería
        // peor: diez invitados apuntando el móvil a la nada.
        var direccion = Servicio().Resolver(navegador);

        Assert.Null(direccion.Raiz);
        Assert.False(direccion.EsUtil);
        Assert.Equal(OrigenDireccionSitio.Ninguna, direccion.Origen);
    }

    [Fact]
    public void La_segunda_direccion_se_enseña_solo_si_es_distinta_de_la_principal()
    {
        var conNombre = Servicio(urlEnEsteEquipo: "https://miscatas.es/")
            .Resolver("http://192.168.2.139:5081/");

        Assert.Equal("https://miscatas.es", conNombre.SoloEnEsteEquipo);
        Assert.True(conNombre.TieneAlternativaLocal);

        // Arrancando con Arrancar.cmd no hay nombre configurado, así que no hay
        // segunda dirección que enseñar y la pantalla no debe reservarle hueco.
        var sinNombre = Servicio().Resolver("http://192.168.2.139:5081/");

        Assert.Null(sinNombre.SoloEnEsteEquipo);
        Assert.False(sinNombre.TieneAlternativaLocal);
    }

    [Fact]
    public void Una_segunda_direccion_igual_a_la_principal_no_se_repite()
    {
        var direccion = Servicio(urlEnEsteEquipo: "http://192.168.2.139:5081")
            .Resolver("http://192.168.2.139:5081/");

        Assert.Equal(direccion.Raiz, direccion.SoloEnEsteEquipo);
        Assert.False(direccion.TieneAlternativaLocal);
    }
}
