using System.Linq.Expressions;
using MisCatas.Domain.Entidades;
using MisCatas.Domain.Enums;

namespace MisCatas.Application.Catas;

/// <summary>
/// Única fuente de verdad de qué significa que una cata esté «activa».
///
/// Regla dura del sistema: SOLO PUEDE HABER UNA CATA ACTIVA A LA VEZ. Una cata
/// activa es la que está <see cref="EstadoCata.Abierta"/> (inscripción abierta,
/// esperando el primer vino) o <see cref="EstadoCata.EnCurso"/> (votándose).
///
/// Esto se valida en el servidor, en todos los caminos que ponen una cata en
/// esos estados: abrir, empezar y reabrir. La interfaz solo lo refleja.
/// </summary>
public static class CataActiva
{
    /// <summary>Estados que ocupan el único hueco de cata activa.</summary>
    public static readonly EstadoCata[] Estados =
    [
        EstadoCata.Abierta,
        EstadoCata.EnCurso
    ];

    /// <summary>True si el estado ocupa el hueco de cata activa.</summary>
    public static bool Es(EstadoCata estado) =>
        estado is EstadoCata.Abierta or EstadoCata.EnCurso;

    /// <summary>Predicado traducible a SQL para filtrar catas activas.</summary>
    public static Expression<Func<Cata, bool>> Filtro =>
        c => c.Estado == EstadoCata.Abierta || c.Estado == EstadoCata.EnCurso;

    /// <summary>
    /// Mensaje único de rechazo cuando ya hay otra cata ocupando el hueco.
    /// Se usa igual en los dos servicios para que el usuario lea siempre lo mismo.
    /// </summary>
    public static string MensajeOcupado(string nombre, EstadoCata estado) =>
        estado == EstadoCata.EnCurso
            ? $"Ya hay una cata en curso: «{nombre}». Finalízala antes de activar otra."
            : $"Ya hay una cata abierta: «{nombre}». Finalízala o devuélvela a borrador antes de activar otra.";
}
