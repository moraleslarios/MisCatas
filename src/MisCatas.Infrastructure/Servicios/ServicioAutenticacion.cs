using Microsoft.EntityFrameworkCore;
using MisCatas.Application.Autenticacion;
using MisCatas.Application.Comun;
using MisCatas.Application.Seguridad;
using MisCatas.Domain.Enums;
using MisCatas.Infrastructure.Persistencia;

namespace MisCatas.Infrastructure.Servicios;

/// <summary>Implementación de <see cref="IServicioAutenticacion"/> sobre EF Core.</summary>
public class ServicioAutenticacion : IServicioAutenticacion
{
    /// <summary>Longitud mínima exigida a una contraseña nueva.</summary>
    public const int LongitudMinimaPassword = 6;

    /// <summary>
    /// Mensaje único para credenciales incorrectas o usuario desactivado:
    /// así no se revela qué logins existen.
    /// </summary>
    private const string ErrorCredenciales = "Usuario o contraseña incorrectos.";

    private readonly IDbContextFactory<AppDbContext> _fabrica;
    private readonly IHasherContrasena _hasher;

    public ServicioAutenticacion(
        IDbContextFactory<AppDbContext> fabrica,
        IHasherContrasena hasher)
    {
        _fabrica = fabrica;
        _hasher = hasher;
    }

    public async Task<Resultado<IdentidadUsuario>> ValidarCredencialesAsync(
        string login,
        string contrasena,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(contrasena))
        {
            return Resultado.Fallo<IdentidadUsuario>(ErrorCredenciales);
        }

        var loginNormalizado = login.Trim();

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var usuario = await db.Usuarios
            .FirstOrDefaultAsync(u => u.Login == loginNormalizado, ct);

        if (usuario is null || !usuario.Activo)
        {
            return Resultado.Fallo<IdentidadUsuario>(ErrorCredenciales);
        }

        var verificacion = _hasher.Verificar(usuario.PasswordHash, contrasena);

        if (verificacion == ResultadoVerificacion.Fallida)
        {
            return Resultado.Fallo<IdentidadUsuario>(ErrorCredenciales);
        }

        // Si el formato del hash es antiguo, se regraba con los parámetros actuales.
        if (verificacion == ResultadoVerificacion.CorrectaNecesitaRehash)
        {
            usuario.PasswordHash = _hasher.Hashear(contrasena);
            await db.SaveChangesAsync(ct);
        }

        return Resultado.Ok(new IdentidadUsuario(
            usuario.Id,
            usuario.Login,
            usuario.NombreCompleto,
            usuario.Rol,
            usuario.DebeCambiarPassword));
    }

    public async Task<Resultado> CambiarPasswordAsync(
        int usuarioId,
        string contrasenaActual,
        string contrasenaNueva,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(contrasenaNueva) ||
            contrasenaNueva.Length < LongitudMinimaPassword)
        {
            return Resultado.Fallo(
                $"La nueva contraseña debe tener al menos {LongitudMinimaPassword} caracteres.");
        }

        await using var db = await _fabrica.CreateDbContextAsync(ct);

        var usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is null)
        {
            return Resultado.Fallo("El usuario no existe.");
        }

        if (_hasher.Verificar(usuario.PasswordHash, contrasenaActual)
            == ResultadoVerificacion.Fallida)
        {
            return Resultado.Fallo("La contraseña actual no es correcta.");
        }

        if (contrasenaNueva == contrasenaActual)
        {
            return Resultado.Fallo("La nueva contraseña debe ser distinta de la actual.");
        }

        usuario.PasswordHash = _hasher.Hashear(contrasenaNueva);
        usuario.DebeCambiarPassword = false;

        await db.SaveChangesAsync(ct);
        return Resultado.Ok();
    }

    public async Task<IdentidadUsuario?> ObtenerIdentidadAsync(
        int usuarioId,
        CancellationToken ct = default)
    {
        await using var db = await _fabrica.CreateDbContextAsync(ct);

        return await db.Usuarios.AsNoTracking()
            .Where(u => u.Id == usuarioId && u.Activo)
            .Select(u => new IdentidadUsuario(
                u.Id,
                u.Login,
                u.Nombre + " " + u.Apellidos,
                u.Rol,
                u.DebeCambiarPassword))
            .FirstOrDefaultAsync(ct);
    }
}
