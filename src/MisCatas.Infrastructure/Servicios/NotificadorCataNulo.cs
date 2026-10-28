using MisCatas.Application.Participacion;
using MisCatas.Application.Votacion;

namespace MisCatas.Infrastructure.Servicios;

/// <summary>
/// Notificador de reserva: traga los eventos sin hacer nada.
///
/// Existe para que la capa de infraestructura se pueda registrar y probar sin
/// arrastrar SignalR. La aplicación web sustituye este registro por el
/// notificador real basado en el hub.
/// </summary>
public sealed class NotificadorCataNulo : INotificadorCata
{
    public Task EnviarEstadoAsync(int cataId, string evento, EstadoVivoDto estado, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task EnviarTickAsync(int cataId, EstadoVivoDto estado, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task EnviarResumenAsync(int cataId, ProgresoCataDto progreso, CancellationToken ct = default) =>
        Task.CompletedTask;
}
