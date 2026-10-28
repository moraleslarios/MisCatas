using Microsoft.EntityFrameworkCore;
using MisCatas.Application.Comun;
using MisCatas.Application.Imagenes;
using MisCatas.Application.Vinos;
using MisCatas.Domain.Entidades;
using MisCatas.Infrastructure.Persistencia;

namespace MisCatas.Infrastructure.Servicios;

/// <summary>Implementación de <see cref="IServicioVinos"/> sobre EF Core.</summary>
public class ServicioVinos : IServicioVinos
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;
    private readonly IServicioImagenes _imagenes;

    public ServicioVinos(
        IDbContextFactory<AppDbContext> fabrica,
        IServicioImagenes imagenes)
    {
        _fabrica = fabrica;
        _imagenes = imagenes;
    }

    public async Task<ResultadoPaginado<VinoDto>> ListarAsync(
        string? busqueda = null,
        int pagina = 1,
        int tamanoPagina = 20,
        CancellationToken ct = default)
    {
        if (pagina < 1) pagina = 1;
        if (tamanoPagina < 1) tamanoPagina = 20;

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var consulta = db.Vinos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var patron = $"%{busqueda.Trim()}%";
            consulta = consulta.Where(v =>
                EF.Functions.Like(v.Nombre, patron) ||
                EF.Functions.Like(v.Dueno, patron) ||
                (v.Bodega != null && EF.Functions.Like(v.Bodega, patron)) ||
                (v.DenominacionOrigen != null && EF.Functions.Like(v.DenominacionOrigen, patron)) ||
                (v.Clase != null && EF.Functions.Like(v.Clase, patron)));
        }

        var total = await consulta.CountAsync(ct);

        var elementos = await consulta
            .OrderBy(v => v.Nombre).ThenBy(v => v.Anio)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(v => new VinoDto(
                v.Id, v.Nombre, v.Anio, v.Clase, v.Dueno, v.PrecioEstimado,
                v.Bodega, v.DenominacionOrigen, v.Comentarios,
                v.Foto != null, v.Etiqueta != null))
            .ToListAsync(ct);

        return new ResultadoPaginado<VinoDto>(elementos, total, pagina, tamanoPagina);
    }

    public async Task<IReadOnlyList<VinoDto>> ListarTodosAsync(CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        return await db.Vinos.AsNoTracking()
            .OrderBy(v => v.Nombre).ThenBy(v => v.Anio)
            .Select(v => new VinoDto(
                v.Id, v.Nombre, v.Anio, v.Clase, v.Dueno, v.PrecioEstimado,
                v.Bodega, v.DenominacionOrigen, v.Comentarios,
                v.Foto != null, v.Etiqueta != null))
            .ToListAsync(ct);
    }

    public async Task<VinoDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        return await db.Vinos.AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new VinoDto(
                v.Id, v.Nombre, v.Anio, v.Clase, v.Dueno, v.PrecioEstimado,
                v.Bodega, v.DenominacionOrigen, v.Comentarios,
                v.Foto != null, v.Etiqueta != null))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Resultado<int>> CrearAsync(
        GuardarVinoDto datos,
        CancellationToken ct = default)
    {
        var validacion = Validar(datos);
        if (validacion.EsFallo)
        {
            return Resultado.Fallo<int>(validacion.Error!);
        }

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var vino = new Vino();
        Volcar(datos, vino);

        db.Vinos.Add(vino);
        await db.SaveChangesAsync(ct);

        return Resultado.Ok(vino.Id);
    }

    public async Task<Resultado> EditarAsync(
        GuardarVinoDto datos,
        CancellationToken ct = default)
    {
        var validacion = Validar(datos);
        if (validacion.EsFallo)
        {
            return validacion;
        }

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var vino = await db.Vinos.FirstOrDefaultAsync(v => v.Id == datos.Id, ct);
        if (vino is null)
        {
            return Resultado.Fallo("El vino no existe.");
        }

        Volcar(datos, vino);
        await db.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    public async Task<Resultado> EliminarAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var vino = await db.Vinos.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (vino is null)
        {
            return Resultado.Fallo("El vino no existe.");
        }

        if (await db.Votos.AnyAsync(v => v.VinoId == id, ct))
        {
            return Resultado.Fallo("El vino tiene votos registrados; no se puede borrar.");
        }

        if (await db.CataVinos.AnyAsync(cv => cv.VinoId == id, ct))
        {
            return Resultado.Fallo(
                "El vino está asignado a alguna cata; quítalo de las catas primero.");
        }

        db.Vinos.Remove(vino);
        await db.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    public async Task<Resultado> GuardarImagenAsync(
        int vinoId,
        TipoImagenVino tipo,
        Stream contenido,
        string nombreArchivo,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var vino = await db.Vinos.FirstOrDefaultAsync(v => v.Id == vinoId, ct);
        if (vino is null)
        {
            return Resultado.Fallo("El vino no existe.");
        }

        ImagenProcesada procesada;
        try
        {
            procesada = await _imagenes.ProcesarAsync(contenido, nombreArchivo, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Resultado.Fallo(ex.Message);
        }

        if (tipo == TipoImagenVino.Foto)
        {
            vino.Foto = procesada.Contenido;
            vino.FotoContentType = procesada.ContentType;
        }
        else
        {
            vino.Etiqueta = procesada.Contenido;
            vino.EtiquetaContentType = procesada.ContentType;
        }

        await db.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> BorrarImagenAsync(
        int vinoId,
        TipoImagenVino tipo,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var vino = await db.Vinos.FirstOrDefaultAsync(v => v.Id == vinoId, ct);
        if (vino is null)
        {
            return Resultado.Fallo("El vino no existe.");
        }

        if (tipo == TipoImagenVino.Foto)
        {
            vino.Foto = null;
            vino.FotoContentType = null;
        }
        else
        {
            vino.Etiqueta = null;
            vino.EtiquetaContentType = null;
        }

        await db.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    public async Task<ImagenVinoDto?> ObtenerImagenAsync(
        int vinoId,
        TipoImagenVino tipo,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        // Se proyecta solo la columna necesaria para no traer las dos imágenes.
        var fila = tipo == TipoImagenVino.Foto
            ? await db.Vinos.AsNoTracking()
                .Where(v => v.Id == vinoId)
                .Select(v => new { Datos = v.Foto, Tipo = v.FotoContentType })
                .FirstOrDefaultAsync(ct)
            : await db.Vinos.AsNoTracking()
                .Where(v => v.Id == vinoId)
                .Select(v => new { Datos = v.Etiqueta, Tipo = v.EtiquetaContentType })
                .FirstOrDefaultAsync(ct);

        if (fila?.Datos is null || fila.Datos.Length == 0)
        {
            return null;
        }

        return new ImagenVinoDto(fila.Datos, fila.Tipo ?? "image/jpeg");
    }

    /// <summary>Valida los campos obligatorios y los rangos de un vino.</summary>
    private static Resultado Validar(GuardarVinoDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        if (string.IsNullOrWhiteSpace(datos.Nombre))
        {
            return Resultado.Fallo("El nombre del vino es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(datos.Dueno))
        {
            return Resultado.Fallo(
                "El dueño es obligatorio: es quien gana la cata si su vino vence.");
        }

        if (datos.Anio is < 1900 or > 2100)
        {
            return Resultado.Fallo("La añada debe estar entre 1900 y 2100.");
        }

        if (datos.PrecioEstimado is < 0)
        {
            return Resultado.Fallo("El precio estimado no puede ser negativo.");
        }

        return Resultado.Ok();
    }

    /// <summary>Copia los datos del DTO a la entidad, recortando espacios.</summary>
    private static void Volcar(GuardarVinoDto datos, Vino vino)
    {
        vino.Nombre = datos.Nombre.Trim();
        vino.Anio = datos.Anio;
        vino.Clase = Normalizar(datos.Clase);
        vino.Dueno = datos.Dueno.Trim();
        vino.PrecioEstimado = datos.PrecioEstimado;
        vino.Bodega = Normalizar(datos.Bodega);
        vino.DenominacionOrigen = Normalizar(datos.DenominacionOrigen);
        vino.Comentarios = Normalizar(datos.Comentarios);
    }

    /// <summary>Recorta un texto opcional y lo convierte a nulo si queda vacío.</summary>
    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
