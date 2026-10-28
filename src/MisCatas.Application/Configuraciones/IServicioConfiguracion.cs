using MisCatas.Application.Comun;
using MisCatas.Domain.Entidades;

namespace MisCatas.Application.Configuraciones;

/// <summary>Acceso a los parámetros globales de la aplicación.</summary>
public interface IServicioConfiguracion
{
    /// <summary>
    /// Obtiene la fila única de configuración. Si no existe la crea con
    /// los valores por defecto.
    /// </summary>
    Task<Configuracion> ObtenerAsync(CancellationToken ct = default);

    /// <summary>Guarda los parámetros globales validando los rangos.</summary>
    Task<Resultado> GuardarAsync(Configuracion datos, CancellationToken ct = default);
}
