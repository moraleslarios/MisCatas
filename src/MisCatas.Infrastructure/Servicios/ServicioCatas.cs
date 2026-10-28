using Microsoft.EntityFrameworkCore;
using MisCatas.Application.Catas;
using MisCatas.Application.Comun;
using MisCatas.Domain.Entidades;
using MisCatas.Domain.Enums;
using MisCatas.Infrastructure.Persistencia;

namespace MisCatas.Infrastructure.Servicios;

/// <summary>Implementación de <see cref="IServicioCatas"/> sobre EF Core.</summary>
public class ServicioCatas : IServicioCatas
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;

    public ServicioCatas(IDbContextFactory<AppDbContext> fabrica)
    {
        _fabrica = fabrica;
    }

    public async Task<ResultadoPaginado<CataDto>> ListarAsync(
        string? busqueda = null,
        EstadoCata? estado = null,
        int pagina = 1,
        int tamanoPagina = 20,
        CancellationToken ct = default)
    {
        if (pagina < 1) pagina = 1;
        if (tamanoPagina < 1) tamanoPagina = 20;

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var consulta = db.Catas.AsNoTracking();

        if (estado.HasValue)
        {
            consulta = consulta.Where(c => c.Estado == estado.Value);
        }

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var patron = $"%{busqueda.Trim()}%";
            consulta = consulta.Where(c =>
                EF.Functions.Like(c.Nombre, patron) ||
                (c.Lugar != null && EF.Functions.Like(c.Lugar, patron)));
        }

        var total = await consulta.CountAsync(ct);

        var elementos = await consulta
            .OrderByDescending(c => c.Fecha).ThenByDescending(c => c.Id)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(c => new CataDto(
                c.Id, c.Nombre, c.Lugar, c.Fecha, c.Estado,
                c.Participantes.Count, c.Vinos.Count,
                c.TiempoVotoSegundos, c.Comentarios))
            .ToListAsync(ct);

        return new ResultadoPaginado<CataDto>(elementos, total, pagina, tamanoPagina);
    }

    public async Task<CataDetalleDto?> ObtenerDetalleAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CataDto(
                c.Id, c.Nombre, c.Lugar, c.Fecha, c.Estado,
                c.Participantes.Count, c.Vinos.Count,
                c.TiempoVotoSegundos, c.Comentarios))
            .FirstOrDefaultAsync(ct);

        if (cata is null)
        {
            return null;
        }

        var vinos = await db.CataVinos.AsNoTracking()
            .Where(cv => cv.CataId == id)
            .OrderBy(cv => cv.Orden)
            .Select(cv => new CataVinoDto(
                cv.Id, cv.VinoId, cv.Vino.Nombre, cv.Vino.Anio,
                cv.Orden, cv.EstadoVotacion))
            .ToListAsync(ct);

        var participantes = await db.CataUsuarios.AsNoTracking()
            .Where(cu => cu.CataId == id)
            .OrderBy(cu => cu.Usuario.Apellidos).ThenBy(cu => cu.Usuario.Nombre)
            .Select(cu => new CataParticipanteDto(
                cu.Id, cu.UsuarioId,
                cu.Usuario.Nombre + " " + cu.Usuario.Apellidos,
                cu.Usuario.Login, cu.Usuario.Rol, cu.Usuario.Telefono))
            .ToListAsync(ct);

        return new CataDetalleDto(cata, vinos, participantes);
    }

    public async Task<Resultado<int>> CrearAsync(
        GuardarCataDto datos,
        CancellationToken ct = default)
    {
        var validacion = Validar(datos);
        if (validacion.EsFallo)
        {
            return Resultado.Fallo<int>(validacion.Error!);
        }

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = new Cata { Estado = EstadoCata.Borrador };
        Volcar(datos, cata);

        db.Catas.Add(cata);
        await db.SaveChangesAsync(ct);

        return Resultado.Ok(cata.Id);
    }

    public async Task<Resultado> EditarAsync(
        GuardarCataDto datos,
        CancellationToken ct = default)
    {
        var validacion = Validar(datos);
        if (validacion.EsFallo)
        {
            return validacion;
        }

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == datos.Id, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (cata.Estado == EstadoCata.Finalizada)
        {
            return Resultado.Fallo("Una cata finalizada no se puede modificar.");
        }

        Volcar(datos, cata);
        await db.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    public async Task<Resultado> EliminarAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (await db.Votos.AnyAsync(v => v.CataId == id, ct))
        {
            return Resultado.Fallo("La cata tiene votos emitidos; no se puede borrar.");
        }

        db.Catas.Remove(cata);
        await db.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    // ------------------------------------------------------------------
    // Vinos de la cata
    // ------------------------------------------------------------------

    public async Task<Resultado> AgregarVinoAsync(
        int cataId,
        int vinoId,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (!EsEditable(cata.Estado))
        {
            return Resultado.Fallo(
                "Solo se pueden cambiar los vinos de una cata en Borrador o Abierta.");
        }

        if (!await db.Vinos.AnyAsync(v => v.Id == vinoId, ct))
        {
            return Resultado.Fallo("El vino no existe.");
        }

        if (await db.CataVinos.AnyAsync(cv => cv.CataId == cataId && cv.VinoId == vinoId, ct))
        {
            return Resultado.Fallo("Ese vino ya está en la cata.");
        }

        var ultimoOrden = await db.CataVinos
            .Where(cv => cv.CataId == cataId)
            .Select(cv => (int?)cv.Orden)
            .MaxAsync(ct) ?? 0;

        db.CataVinos.Add(new CataVino
        {
            CataId = cataId,
            VinoId = vinoId,
            Orden = ultimoOrden + 1,
            EstadoVotacion = EstadoVotacion.Pendiente
        });

        await db.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> QuitarVinoAsync(
        int cataId,
        int vinoId,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (!EsEditable(cata.Estado))
        {
            return Resultado.Fallo(
                "Solo se pueden cambiar los vinos de una cata en Borrador o Abierta.");
        }

        var asignacion = await db.CataVinos
            .FirstOrDefaultAsync(cv => cv.CataId == cataId && cv.VinoId == vinoId, ct);

        if (asignacion is null)
        {
            return Resultado.Fallo("Ese vino no está en la cata.");
        }

        if (await db.Votos.AnyAsync(v => v.CataId == cataId && v.VinoId == vinoId, ct))
        {
            return Resultado.Fallo("Ese vino ya tiene votos en la cata; no se puede quitar.");
        }

        if (cata.VinoActualId == vinoId)
        {
            cata.VinoActualId = null;
        }

        db.CataVinos.Remove(asignacion);
        await db.SaveChangesAsync(ct);

        // Recompacta los órdenes para que queden consecutivos desde 1.
        await RecompactarOrdenesAsync(db, cataId, ct);

        return Resultado.Ok();
    }

    public async Task<Resultado> ReordenarVinosAsync(
        int cataId,
        IReadOnlyList<int> vinoIdsEnOrden,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(vinoIdsEnOrden);

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (!EsEditable(cata.Estado))
        {
            return Resultado.Fallo(
                "Solo se puede reordenar una cata en Borrador o Abierta.");
        }

        var asignaciones = await db.CataVinos
            .Where(cv => cv.CataId == cataId)
            .ToListAsync(ct);

        if (asignaciones.Count != vinoIdsEnOrden.Count ||
            vinoIdsEnOrden.Distinct().Count() != vinoIdsEnOrden.Count ||
            vinoIdsEnOrden.Any(id => asignaciones.All(a => a.VinoId != id)))
        {
            return Resultado.Fallo(
                "La lista de orden debe contener exactamente los vinos de la cata.");
        }

        // Dos pasadas, igual que en el reordenado manual: primero se mueven a
        // órdenes negativos para no chocar con el índice único (CataId, Orden)
        // durante el intercambio. No se abre una transacción a mano porque el
        // contexto usa la estrategia de reintentos de SQL Server, que no las
        // admite.
        foreach (var asignacion in asignaciones)
        {
            asignacion.Orden = -asignacion.Orden;
        }

        await db.SaveChangesAsync(ct);

        for (var indice = 0; indice < vinoIdsEnOrden.Count; indice++)
        {
            var asignacion = asignaciones.First(a => a.VinoId == vinoIdsEnOrden[indice]);
            asignacion.Orden = indice + 1;
        }

        await db.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> OrdenarVinosAlAzarAsync(
        int cataId,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (!EsEditable(cata.Estado))
        {
            return Resultado.Fallo(
                "Solo se puede barajar el orden de una cata en Borrador o Abierta.");
        }

        var asignaciones = await db.CataVinos
            .Where(cv => cv.CataId == cataId)
            .ToListAsync(ct);

        if (asignaciones.Count < 2)
        {
            return Resultado.Fallo(
                "Hacen falta al menos dos vinos para poder barajar el orden.");
        }

        // No se baraja si la cata ya ha empezado a rodar: cambiar el orden
        // dejaría los votos ya emitidos apuntando a posiciones distintas.
        if (asignaciones.Any(a => a.EstadoVotacion != EstadoVotacion.Pendiente))
        {
            return Resultado.Fallo(
                "No se puede barajar: ya hay vinos lanzados o cerrados en esta cata.");
        }

        if (await db.Votos.AnyAsync(v => v.CataId == cataId, ct))
        {
            return Resultado.Fallo(
                "No se puede barajar: ya hay votos registrados en esta cata.");
        }

        // Fisher-Yates sobre los identificadores de vino.
        var ordenAlAzar = asignaciones.Select(a => a.VinoId).ToList();
        for (var i = ordenAlAzar.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (ordenAlAzar[i], ordenAlAzar[j]) = (ordenAlAzar[j], ordenAlAzar[i]);
        }

        // Primera pasada: órdenes negativos para no chocar con el índice
        // único (CataId, Orden) durante el intercambio. No se abre una
        // transacción a mano porque el contexto usa la estrategia de
        // reintentos de SQL Server, que no las admite.
        foreach (var asignacion in asignaciones)
        {
            asignacion.Orden = -asignacion.Orden;
        }

        await db.SaveChangesAsync(ct);

        for (var indice = 0; indice < ordenAlAzar.Count; indice++)
        {
            var asignacion = asignaciones.First(a => a.VinoId == ordenAlAzar[indice]);
            asignacion.Orden = indice + 1;
        }

        await db.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    // ------------------------------------------------------------------
    // Participantes
    // ------------------------------------------------------------------

    public async Task<Resultado> AgregarParticipanteAsync(
        int cataId,
        int usuarioId,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (cata.Estado == EstadoCata.Finalizada)
        {
            return Resultado.Fallo("Una cata finalizada no admite cambios.");
        }

        var usuario = await db.Usuarios.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == usuarioId, ct);

        if (usuario is null)
        {
            return Resultado.Fallo("El usuario no existe.");
        }

        if (!usuario.Activo)
        {
            return Resultado.Fallo("El usuario está desactivado.");
        }

        // A las catas solo se invita a participantes normales: ni los administradores
        // ni el usuario escaparate votan.
        if (usuario.Rol != Rol.Usuario)
        {
            return Resultado.Fallo("Solo se puede invitar a usuarios con el rol Usuario.");
        }

        if (await db.CataUsuarios.AnyAsync(
                cu => cu.CataId == cataId && cu.UsuarioId == usuarioId, ct))
        {
            return Resultado.Fallo("Ese usuario ya está invitado a la cata.");
        }

        db.CataUsuarios.Add(new CataUsuario { CataId = cataId, UsuarioId = usuarioId });
        await db.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    public async Task<Resultado> QuitarParticipanteAsync(
        int cataId,
        int usuarioId,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cataId, ct);

        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (cata.Estado == EstadoCata.Finalizada)
        {
            return Resultado.Fallo("Una cata finalizada no admite cambios.");
        }

        var invitacion = await db.CataUsuarios
            .FirstOrDefaultAsync(cu => cu.CataId == cataId && cu.UsuarioId == usuarioId, ct);

        if (invitacion is null)
        {
            return Resultado.Fallo("Ese usuario no está invitado a la cata.");
        }

        if (await db.Votos.AnyAsync(v => v.CataId == cataId && v.UsuarioId == usuarioId, ct))
        {
            return Resultado.Fallo("Ese usuario ya ha votado en la cata; no se puede quitar.");
        }

        db.CataUsuarios.Remove(invitacion);
        await db.SaveChangesAsync(ct);

        return Resultado.Ok();
    }

    public async Task<Resultado> EstablecerParticipantesAsync(
        int cataId,
        IReadOnlyList<int> usuarioIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(usuarioIds);

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cataId, ct);

        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (cata.Estado == EstadoCata.Finalizada)
        {
            return Resultado.Fallo("Una cata finalizada no admite cambios.");
        }

        var deseados = usuarioIds.Distinct().ToHashSet();

        var validos = await db.Usuarios.AsNoTracking()
            .Where(u => deseados.Contains(u.Id) && u.Activo && u.Rol != Rol.Escaparate)
            .Select(u => u.Id)
            .ToListAsync(ct);

        if (validos.Count != deseados.Count)
        {
            return Resultado.Fallo(
                "Alguno de los usuarios seleccionados no existe, está desactivado " +
                "o es el usuario escaparate.");
        }

        var actuales = await db.CataUsuarios
            .Where(cu => cu.CataId == cataId)
            .ToListAsync(ct);

        var aQuitar = actuales.Where(cu => !deseados.Contains(cu.UsuarioId)).ToList();

        if (aQuitar.Count > 0)
        {
            var idsAQuitar = aQuitar.Select(cu => cu.UsuarioId).ToList();

            if (await db.Votos.AnyAsync(
                    v => v.CataId == cataId && idsAQuitar.Contains(v.UsuarioId), ct))
            {
                return Resultado.Fallo(
                    "Alguno de los usuarios que quieres retirar ya ha votado en la cata.");
            }

            db.CataUsuarios.RemoveRange(aQuitar);
        }

        var yaInvitados = actuales.Select(cu => cu.UsuarioId).ToHashSet();

        foreach (var usuarioId in deseados.Where(id => !yaInvitados.Contains(id)))
        {
            db.CataUsuarios.Add(new CataUsuario { CataId = cataId, UsuarioId = usuarioId });
        }

        await db.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------
    // Ciclo de vida
    // ------------------------------------------------------------------

    public async Task<CataDto?> ObtenerCataActivaAsync(CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        // Si por lo que fuera hubiese más de una, manda la que esté EnCurso y,
        // dentro de ellas, la abierta más recientemente.
        return await db.Catas
            .AsNoTracking()
            .Where(CataActiva.Filtro)
            .OrderByDescending(c => c.Estado)
            .ThenByDescending(c => c.FechaApertura ?? DateTime.MinValue)
            .ThenByDescending(c => c.Id)
            .Select(c => new CataDto(
                c.Id, c.Nombre, c.Lugar, c.Fecha, c.Estado,
                c.Participantes.Count, c.Vinos.Count,
                c.TiempoVotoSegundos, c.Comentarios))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Resultado> CambiarEstadoAsync(
        int cataId,
        EstadoCata nuevoEstado,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var cata = await db.Catas.FirstOrDefaultAsync(c => c.Id == cataId, ct);
        if (cata is null)
        {
            return Resultado.Fallo("La cata no existe.");
        }

        if (cata.Estado == nuevoEstado)
        {
            return Resultado.Ok();
        }

        if (!TransicionPermitida(cata.Estado, nuevoEstado))
        {
            return Resultado.Fallo(
                $"No se puede pasar de «{cata.Estado}» a «{nuevoEstado}».");
        }

        if (CataActiva.Es(nuevoEstado))
        {
            // Regla dura: solo una cata activa a la vez. Se comprueba aquí, en
            // el servidor, y no en el botón de la pantalla.
            var ocupada = await db.Catas
                .AsNoTracking()
                .Where(CataActiva.Filtro)
                .Where(c => c.Id != cataId)
                .OrderByDescending(c => c.Estado)
                .Select(c => new { c.Nombre, c.Estado })
                .FirstOrDefaultAsync(ct);

            if (ocupada is not null)
            {
                return Resultado.Fallo(CataActiva.MensajeOcupado(ocupada.Nombre, ocupada.Estado));
            }

            if (!await db.CataVinos.AnyAsync(cv => cv.CataId == cataId, ct))
            {
                return Resultado.Fallo("La cata no tiene vinos asignados.");
            }

            if (!await db.CataUsuarios.AnyAsync(cu => cu.CataId == cataId, ct))
            {
                return Resultado.Fallo("La cata no tiene participantes.");
            }
        }

        cata.Estado = nuevoEstado;

        switch (nuevoEstado)
        {
            case EstadoCata.Abierta:
                cata.FechaApertura ??= DateTime.UtcNow;
                break;

            case EstadoCata.Finalizada:
                cata.FechaFinalizacion = DateTime.UtcNow;
                cata.VinoActualId = null;
                cata.RevelacionActiva = false;
                break;

            case EstadoCata.Borrador:
                cata.FechaApertura = null;
                cata.FechaFinalizacion = null;
                cata.VinoActualId = null;
                cata.RevelacionActiva = false;
                break;
        }

        await db.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    // ------------------------------------------------------------------
    // Auxiliares
    // ------------------------------------------------------------------

    /// <summary>Estados en los que el administrador puede cambiar vinos y orden.</summary>
    private static bool EsEditable(EstadoCata estado) =>
        estado is EstadoCata.Borrador or EstadoCata.Abierta;

    /// <summary>Transiciones válidas del ciclo de vida de una cata.</summary>
    private static bool TransicionPermitida(EstadoCata actual, EstadoCata nuevo) =>
        (actual, nuevo) switch
        {
            (EstadoCata.Borrador, EstadoCata.Abierta) => true,
            (EstadoCata.Abierta, EstadoCata.Borrador) => true,
            (EstadoCata.Abierta, EstadoCata.EnCurso) => true,
            (EstadoCata.EnCurso, EstadoCata.Finalizada) => true,
            (EstadoCata.Abierta, EstadoCata.Finalizada) => true,
            _ => false
        };

    /// <summary>Deja los órdenes de los vinos consecutivos empezando en 1.</summary>
    private static async Task RecompactarOrdenesAsync(
        AppDbContext db,
        int cataId,
        CancellationToken ct)
    {
        var asignaciones = await db.CataVinos
            .Where(cv => cv.CataId == cataId)
            .OrderBy(cv => cv.Orden)
            .ToListAsync(ct);

        var hayCambios = asignaciones
            .Where((a, i) => a.Orden != i + 1)
            .Any();

        if (!hayCambios)
        {
            return;
        }

        // Primero a negativos para no violar el índice único (CataId, Orden).
        foreach (var asignacion in asignaciones)
        {
            asignacion.Orden = -asignacion.Orden;
        }

        await db.SaveChangesAsync(ct);

        for (var indice = 0; indice < asignaciones.Count; indice++)
        {
            asignaciones[indice].Orden = indice + 1;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Valida los datos generales de una cata.</summary>
    private static Resultado Validar(GuardarCataDto datos)
    {
        ArgumentNullException.ThrowIfNull(datos);

        if (string.IsNullOrWhiteSpace(datos.Nombre))
        {
            return Resultado.Fallo("El nombre de la cata es obligatorio.");
        }

        if (datos.TiempoVotoSegundos is < 10 or > 3600)
        {
            return Resultado.Fallo(
                "El tiempo de voto debe estar entre 10 y 3600 segundos, " +
                "o quedar vacío para usar el valor global.");
        }

        return Resultado.Ok();
    }

    /// <summary>Copia los datos del DTO a la entidad.</summary>
    private static void Volcar(GuardarCataDto datos, Cata cata)
    {
        cata.Nombre = datos.Nombre.Trim();
        cata.Lugar = string.IsNullOrWhiteSpace(datos.Lugar) ? null : datos.Lugar.Trim();
        cata.Fecha = datos.Fecha.Date;
        cata.Comentarios = string.IsNullOrWhiteSpace(datos.Comentarios)
            ? null
            : datos.Comentarios.Trim();
        cata.TiempoVotoSegundos = datos.TiempoVotoSegundos;
    }
}
