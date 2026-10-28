using Microsoft.EntityFrameworkCore;
using MisCatas.Application.Comun;
using MisCatas.Application.Configuraciones;
using MisCatas.Domain.Entidades;
using MisCatas.Domain.Enums;
using MisCatas.Infrastructure.Persistencia;

namespace MisCatas.Infrastructure.Servicios;

/// <summary>Implementación de <see cref="IServicioConfiguracion"/> sobre EF Core.</summary>
public class ServicioConfiguracion : IServicioConfiguracion
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;

    public ServicioConfiguracion(IDbContextFactory<AppDbContext> fabrica)
    {
        _fabrica = fabrica;
    }

    public async Task<Configuracion> ObtenerAsync(CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var configuracion = await db.Configuraciones.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == Configuracion.IdUnico, ct);

        if (configuracion is not null)
        {
            return configuracion;
        }

        // La fila la crea el seed al arrancar; esto es solo una red de seguridad.
        configuracion = new Configuracion { Id = Configuracion.IdUnico };
        db.Configuraciones.Add(configuracion);
        await db.SaveChangesAsync(ct);

        return configuracion;
    }

    public async Task<Resultado> GuardarAsync(
        Configuracion datos,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var validacion = Validar(datos);
        if (validacion.EsFallo)
        {
            return validacion;
        }

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var actual = await db.Configuraciones
            .FirstOrDefaultAsync(c => c.Id == Configuracion.IdUnico, ct);

        if (actual is null)
        {
            actual = new Configuracion { Id = Configuracion.IdUnico };
            db.Configuraciones.Add(actual);
        }

        actual.TiempoVotoSegundos = datos.TiempoVotoSegundos;
        actual.PorcentajeAlertaRoja = datos.PorcentajeAlertaRoja;
        actual.SegundosRevelacionFinal = datos.SegundosRevelacionFinal;
        actual.PermitirAvanzarSinTodosLosVotos = datos.PermitirAvanzarSinTodosLosVotos;
        actual.PoliticaNoVoto = datos.PoliticaNoVoto;
        actual.NotaPorDefectoNoVoto = datos.PoliticaNoVoto == PoliticaNoVoto.NotaFija
            ? datos.NotaPorDefectoNoVoto
            : null;
        actual.ComentarioAutomaticoNoVoto =
            string.IsNullOrWhiteSpace(datos.ComentarioAutomaticoNoVoto)
                ? "No votado"
                : datos.ComentarioAutomaticoNoVoto.Trim();
        actual.PasoDecimalNota = datos.PasoDecimalNota;

        await db.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    /// <summary>Valida los rangos de los parámetros globales.</summary>
    private static Resultado Validar(Configuracion datos)
    {
        if (datos.TiempoVotoSegundos is < 10 or > 3600)
        {
            return Resultado.Fallo("El tiempo de voto debe estar entre 10 y 3600 segundos.");
        }

        if (datos.PorcentajeAlertaRoja is < 1 or > 100)
        {
            return Resultado.Fallo("El porcentaje de alerta roja debe estar entre 1 y 100.");
        }

        if (datos.SegundosRevelacionFinal is < 0 or > 600)
        {
            return Resultado.Fallo(
                "Los segundos de revelación final deben estar entre 0 y 600.");
        }

        if (datos.PoliticaNoVoto == PoliticaNoVoto.NotaFija)
        {
            if (datos.NotaPorDefectoNoVoto is null)
            {
                return Resultado.Fallo(
                    "Con la política de nota fija hay que indicar la nota por defecto.");
            }

            if (datos.NotaPorDefectoNoVoto is < 0m or > 10m)
            {
                return Resultado.Fallo("La nota por defecto debe estar entre 0,0 y 10,0.");
            }
        }

        decimal[] pasosValidos = [0.1m, 0.25m, 0.5m, 1m];
        if (!pasosValidos.Contains(datos.PasoDecimalNota))
        {
            return Resultado.Fallo(
                "El paso decimal de la nota debe ser 0,1, 0,25, 0,5 o 1.");
        }

        return Resultado.Ok();
    }
}
