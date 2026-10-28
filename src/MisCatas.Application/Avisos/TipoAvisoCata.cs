namespace MisCatas.Application.Avisos;

/// <summary>Clase de aviso que se quiere mandar a los participantes.</summary>
public enum TipoAvisoCata
{
    /// <summary>
    /// Convocatoria: cuándo, dónde, cuántos vinos y cómo entrar en la web.
    /// Es lo que se manda antes de que la cata empiece.
    /// </summary>
    Invitacion = 0,

    /// <summary>
    /// Aviso de que ya están los resultados, con el enlace directo a la
    /// clasificación. Solo tiene sentido con la cata finalizada.
    /// </summary>
    Resultados = 1
}
