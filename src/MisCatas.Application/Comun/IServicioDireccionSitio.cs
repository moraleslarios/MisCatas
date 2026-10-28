namespace MisCatas.Application.Comun;

/// <summary>
/// Averigua por qué dirección tienen que entrar los invitados a la aplicación.
///
/// La idea es que no haya que configurar nada a mano salvo en casos raros: si
/// el organizador ha entrado al panel desde la dirección de red del servidor,
/// esa misma dirección sirve para todos los demás. Y si ha entrado por
/// «localhost» (que en el móvil de otra persona apunta a su propio móvil), la
/// aplicación sustituye ese trozo por la dirección del equipo en la red.
/// </summary>
public interface IServicioDireccionSitio
{
    /// <summary>
    /// Dirección escrita a mano en la configuración, o <c>null</c> si no se ha
    /// puesto ninguna. Manda sobre cualquier otra: si alguien se ha molestado
    /// en escribirla, es porque sabe algo que la aplicación no puede deducir
    /// (un dominio público, un puerto redirigido en el router...).
    /// </summary>
    string? Configurada { get; }

    /// <summary>
    /// Dirección del equipo en la red local (por ejemplo <c>192.168.1.50</c>),
    /// o <c>null</c> si no se ha podido averiguar.
    /// </summary>
    string? IpRedLocal { get; }

    /// <summary>
    /// Decide la mejor dirección para repartir, partiendo de aquella por la
    /// que está navegando ahora mismo quien lo pregunta.
    /// </summary>
    /// <param name="direccionNavegador">
    /// Dirección base del navegador actual, tal como la da
    /// <c>NavigationManager.BaseUri</c>.
    /// </param>
    DireccionSitio Resolver(string? direccionNavegador);
}

/// <summary>
/// Dirección elegida para repartir, junto con el motivo por el que se eligió.
/// El motivo importa porque la pantalla avisa de forma distinta según el caso.
/// </summary>
/// <param name="Raiz">
/// Dirección sin barra final (por ejemplo <c>http://192.168.1.50:5080</c>), o
/// <c>null</c> si no se ha podido determinar ninguna de fiar.
/// </param>
/// <param name="Origen">De dónde ha salido la dirección.</param>
public readonly record struct DireccionSitio(string? Raiz, OrigenDireccionSitio Origen)
{
    /// <summary>Hay una dirección que se puede repartir.</summary>
    public bool EsUtil => Raiz is not null;
}

/// <summary>De dónde sale la dirección que se reparte a los invitados.</summary>
public enum OrigenDireccionSitio
{
    /// <summary>No se ha podido averiguar ninguna dirección de fiar.</summary>
    Ninguna = 0,

    /// <summary>Escrita a mano en la configuración del sitio.</summary>
    Configurada = 1,

    /// <summary>
    /// La misma por la que está entrando quien lo pregunta, porque ya es
    /// alcanzable desde otros equipos.
    /// </summary>
    Navegador = 2,

    /// <summary>
    /// Deducida: quien pregunta entró por «localhost», así que se ha cambiado
    /// por la dirección del equipo en la red local.
    /// </summary>
    RedLocal = 3,
}
