using Microsoft.EntityFrameworkCore;
using MisCatas.Application.Comun;
using MisCatas.Application.Seguridad;
using MisCatas.Application.Usuarios;
using MisCatas.Domain.Entidades;
using MisCatas.Domain.Enums;
using MisCatas.Infrastructure.Persistencia;

namespace MisCatas.Infrastructure.Servicios;

/// <summary>Implementación de <see cref="IServicioUsuarios"/> sobre EF Core.</summary>
public class ServicioUsuarios : IServicioUsuarios
{
    private readonly IDbContextFactory<AppDbContext> _fabrica;
    private readonly IHasherContrasena _hasher;

    public ServicioUsuarios(
        IDbContextFactory<AppDbContext> fabrica,
        IHasherContrasena hasher)
    {
        _fabrica = fabrica;
        _hasher = hasher;
    }

    public async Task<ResultadoPaginado<UsuarioDto>> ListarAsync(
        string? busqueda = null,
        int pagina = 1,
        int tamanoPagina = 20,
        bool incluirInactivos = true,
        CancellationToken ct = default)
    {
        if (pagina < 1) pagina = 1;
        if (tamanoPagina < 1) tamanoPagina = 20;

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var consulta = db.Usuarios.AsNoTracking();

        if (!incluirInactivos)
        {
            consulta = consulta.Where(u => u.Activo);
        }

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var patron = $"%{busqueda.Trim()}%";
            consulta = consulta.Where(u =>
                EF.Functions.Like(u.Login, patron) ||
                EF.Functions.Like(u.Nombre, patron) ||
                EF.Functions.Like(u.Apellidos, patron) ||
                EF.Functions.Like(u.Telefono, patron));
        }

        var total = await consulta.CountAsync(ct);

        var elementos = await consulta
            .OrderBy(u => u.Apellidos).ThenBy(u => u.Nombre)
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Select(u => new UsuarioDto(
                u.Id, u.Login, u.Nombre, u.Apellidos, u.Telefono,
                u.Rol, u.DebeCambiarPassword, u.Activo))
            .ToListAsync(ct);

        return new ResultadoPaginado<UsuarioDto>(elementos, total, pagina, tamanoPagina);
    }

    public async Task<IReadOnlyList<UsuarioDto>> ListarActivosAsync(CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        return await db.Usuarios.AsNoTracking()
            .Where(u => u.Activo)
            .OrderBy(u => u.Apellidos).ThenBy(u => u.Nombre)
            .Select(u => new UsuarioDto(
                u.Id, u.Login, u.Nombre, u.Apellidos, u.Telefono,
                u.Rol, u.DebeCambiarPassword, u.Activo))
            .ToListAsync(ct);
    }

    public async Task<UsuarioDto?> ObtenerAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        return await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UsuarioDto(
                u.Id, u.Login, u.Nombre, u.Apellidos, u.Telefono,
                u.Rol, u.DebeCambiarPassword, u.Activo))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string> SugerirLoginAsync(
        string nombre,
        string apellidos,
        CancellationToken ct = default)
    {
        var baseLogin = Texto.GenerarBaseLogin(nombre, apellidos);
        if (string.IsNullOrEmpty(baseLogin))
        {
            baseLogin = "usuario";
        }

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        // Trae los logins que empiezan por la base para no consultar en bucle.
        var existentes = await db.Usuarios.AsNoTracking()
            .Where(u => u.Login.StartsWith(baseLogin))
            .Select(u => u.Login)
            .ToListAsync(ct);

        var ocupados = existentes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!ocupados.Contains(baseLogin))
        {
            return baseLogin;
        }

        for (var sufijo = 2; sufijo < 1000; sufijo++)
        {
            var candidato = $"{baseLogin}{sufijo}";
            if (!ocupados.Contains(candidato))
            {
                return candidato;
            }
        }

        return $"{baseLogin}{Guid.NewGuid():N}"[..20];
    }

    public async Task<Resultado<int>> CrearAsync(
        CrearUsuarioDto datos,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var nombre = datos.Nombre?.Trim() ?? string.Empty;
        var apellidos = datos.Apellidos?.Trim() ?? string.Empty;
        var telefono = Texto.NormalizarTelefono(datos.Telefono);

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Resultado.Fallo<int>("El nombre es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(apellidos))
        {
            return Resultado.Fallo<int>("Los apellidos son obligatorios.");
        }

        if (telefono.Length < 6)
        {
            return Resultado.Fallo<int>(
                "El teléfono es obligatorio y debe tener al menos 6 dígitos: " +
                "es la contraseña inicial del usuario.");
        }

        var login = string.IsNullOrWhiteSpace(datos.Login)
            ? await SugerirLoginAsync(nombre, apellidos, ct)
            : datos.Login.Trim();

        // Regla de negocio: los participantes entran con su número de teléfono.
        if (datos.Rol == Rol.Usuario)
        {
            login = telefono;
        }

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        if (await db.Usuarios.AnyAsync(u => u.Login == login, ct))
        {
            return Resultado.Fallo<int>($"El login «{login}» ya está en uso.");
        }

        var usuario = new Usuario
        {
            Login = login,
            Nombre = nombre,
            Apellidos = apellidos,
            Telefono = telefono,
            Rol = datos.Rol,
            Activo = datos.Activo,
            PasswordHash = _hasher.Hashear(telefono),
            // El escaparate es un panel fijo: no se le puede pedir que cambie nada.
            DebeCambiarPassword = datos.Rol != Rol.Escaparate
        };

        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(ct);

        return Resultado.Ok(usuario.Id);
    }

    public async Task<Resultado> EditarAsync(
        EditarUsuarioDto datos,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(datos);

        var nombre = datos.Nombre?.Trim() ?? string.Empty;
        var apellidos = datos.Apellidos?.Trim() ?? string.Empty;
        var telefono = Texto.NormalizarTelefono(datos.Telefono);
        var login = datos.Login?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(login))
        {
            return Resultado.Fallo("El login es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(nombre))
        {
            return Resultado.Fallo("El nombre es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(apellidos))
        {
            return Resultado.Fallo("Los apellidos son obligatorios.");
        }

        if (telefono.Length < 6)
        {
            return Resultado.Fallo("El teléfono debe tener al menos 6 dígitos.");
        }

        // Regla de negocio: los participantes entran con su número de teléfono.
        if (datos.Rol == Rol.Usuario)
        {
            login = telefono;
        }

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == datos.Id, ct);
        if (usuario is null)
        {
            return Resultado.Fallo("El usuario no existe.");
        }

        if (await db.Usuarios.AnyAsync(u => u.Login == login && u.Id != datos.Id, ct))
        {
            return Resultado.Fallo($"El login «{login}» ya está en uso.");
        }

        // No dejar el sistema sin ningún administrador activo.
        if (usuario.Rol == Rol.Admin && (datos.Rol != Rol.Admin || !datos.Activo))
        {
            var otrosAdmins = await db.Usuarios.CountAsync(
                u => u.Rol == Rol.Admin && u.Activo && u.Id != usuario.Id, ct);

            if (otrosAdmins == 0)
            {
                return Resultado.Fallo(
                    "No se puede dejar el sistema sin ningún administrador activo.");
            }
        }

        usuario.Login = login;
        usuario.Nombre = nombre;
        usuario.Apellidos = apellidos;

        // Si a un participante le cambia el teléfono, también le cambia el login:
        // su contraseña vuelve a ser el teléfono nuevo y se le pide cambiarla.
        var telefonoCambiado = !string.Equals(usuario.Telefono, telefono, StringComparison.Ordinal);

        usuario.Telefono = telefono;
        usuario.Rol = datos.Rol;
        usuario.Activo = datos.Activo;

        if (datos.Rol == Rol.Usuario && telefonoCambiado)
        {
            usuario.PasswordHash = _hasher.Hashear(telefono);
            usuario.DebeCambiarPassword = true;
        }

        if (datos.Rol == Rol.Escaparate)
        {
            usuario.DebeCambiarPassword = false;
        }

        await db.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> ResetearPasswordAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null)
        {
            return Resultado.Fallo("El usuario no existe.");
        }

        usuario.PasswordHash = _hasher.Hashear(usuario.Telefono);
        usuario.DebeCambiarPassword = usuario.Rol != Rol.Escaparate;

        await db.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> CambiarActivoAsync(
        int id,
        bool activo,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null)
        {
            return Resultado.Fallo("El usuario no existe.");
        }

        if (!activo && usuario.Rol == Rol.Admin)
        {
            var otrosAdmins = await db.Usuarios.CountAsync(
                u => u.Rol == Rol.Admin && u.Activo && u.Id != id, ct);

            if (otrosAdmins == 0)
            {
                return Resultado.Fallo(
                    "No se puede desactivar al único administrador activo.");
            }
        }

        usuario.Activo = activo;
        await db.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    public async Task<Resultado> EliminarAsync(int id, CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null)
        {
            return Resultado.Fallo("El usuario no existe.");
        }

        if (await db.Votos.AnyAsync(v => v.UsuarioId == id, ct))
        {
            return Resultado.Fallo(
                "El usuario tiene votos registrados; no se puede borrar. " +
                "Desactívalo en su lugar.");
        }

        if (await db.CataUsuarios.AnyAsync(cu => cu.UsuarioId == id, ct))
        {
            return Resultado.Fallo(
                "El usuario está invitado a alguna cata; quítalo de las catas " +
                "o desactívalo en su lugar.");
        }

        if (await db.Catas.AnyAsync(c => c.AdminControlaId == id, ct))
        {
            return Resultado.Fallo(
                "El usuario controla alguna cata; no se puede borrar.");
        }

        if (usuario.Rol == Rol.Admin)
        {
            var otrosAdmins = await db.Usuarios.CountAsync(
                u => u.Rol == Rol.Admin && u.Activo && u.Id != id, ct);

            if (otrosAdmins == 0)
            {
                return Resultado.Fallo(
                    "No se puede borrar al único administrador activo.");
            }
        }

        db.Usuarios.Remove(usuario);
        await db.SaveChangesAsync(ct);
        return Resultado.Ok();
    }
}
