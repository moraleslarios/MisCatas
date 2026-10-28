namespace MisCatas.Domain.Enums;

/// <summary>
/// Qué hacer cuando un participante no vota un vino dentro del tiempo disponible.
/// </summary>
public enum PoliticaNoVoto
{
    /// <summary>
    /// El no-voto no se tiene en cuenta para la media (valor por defecto).
    /// Es el defecto deliberadamente: asignar un 0 hundiría la media de un vino por un simple olvido.
    /// </summary>
    NoCuenta = 0,

    /// <summary>
    /// Se registra un voto automático con la nota indicada en
    /// <c>Configuracion.NotaPorDefectoNoVoto</c>.
    /// </summary>
    NotaFija = 1
}
