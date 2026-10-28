using Microsoft.EntityFrameworkCore;
using MisCatas.Application.Comun;
using MisCatas.Application.Seguridad;
using MisCatas.Domain.Entidades;
using MisCatas.Domain.Enums;

namespace MisCatas.Infrastructure.Persistencia;

/// <summary>
/// Datos mínimos imprescindibles al arrancar: la fila de configuración y
/// un administrador inicial. Es idempotente: nunca borra ni sobrescribe
/// nada que ya exista.
/// </summary>
public static class SemillaDatos
{
    /// <summary>Login del administrador que se crea en una base vacía.</summary>
    public const string LoginAdminInicial = "admin";

    /// <summary>Contraseña inicial del administrador (también su «teléfono»).</summary>
    public const string PasswordAdminInicial = "123456";

    /// <summary>Login del usuario de la pantalla de escaparate.</summary>
    public const string LoginEscaparate = "escaparate";

    /// <summary>Contraseña del usuario escaparate.</summary>
    public const string PasswordEscaparate = "escaparate";

    /// <summary>
    /// Crea lo que falte. Se llama después de <c>Database.Migrate()</c>.
    /// </summary>
    public static async Task EjecutarAsync(
        AppDbContext db,
        IHasherContrasena hasher,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(hasher);

        var huboCambios = false;

        // --- Fila única de configuración ---
        if (!await db.Configuraciones.AnyAsync(c => c.Id == Configuracion.IdUnico, ct))
        {
            db.Configuraciones.Add(new Configuracion { Id = Configuracion.IdUnico });
            huboCambios = true;
        }

        // --- Administrador inicial, solo si no hay ningún admin ---
        if (!await db.Usuarios.AnyAsync(u => u.Rol == Rol.Admin, ct))
        {
            db.Usuarios.Add(new Usuario
            {
                Login = LoginAdminInicial,
                Nombre = "Administrador",
                Apellidos = "del Sistema",
                Telefono = PasswordAdminInicial,
                Rol = Rol.Admin,
                PasswordHash = hasher.Hashear(PasswordAdminInicial),
                // Obliga a cambiarla en el primer inicio de sesión.
                DebeCambiarPassword = true,
                Activo = true
            });

            huboCambios = true;
        }

        // --- Usuario escaparate, solo si no existe ---
        if (!await db.Usuarios.AnyAsync(u => u.Rol == Rol.Escaparate, ct))
        {
            db.Usuarios.Add(new Usuario
            {
                Login = LoginEscaparate,
                Nombre = "Pantalla",
                Apellidos = "Escaparate",
                Telefono = PasswordEscaparate,
                Rol = Rol.Escaparate,
                PasswordHash = hasher.Hashear(PasswordEscaparate),
                // El escaparate es un panel fijo: nunca se le pide cambiar nada.
                DebeCambiarPassword = false,
                Activo = true
            });

            huboCambios = true;
        }

        if (huboCambios)
        {
            await db.SaveChangesAsync(ct);
        }

        // --- Logins de los participantes alineados con su teléfono ---
        await SincronizarLoginsUsuariosAsync(db, hasher, ct);
    }

    /// <summary>
    /// Pone el teléfono como login de todos los usuarios con rol
    /// <see cref="Rol.Usuario"/> y restablece su contraseña a ese mismo
    /// teléfono, forzando el cambio en el siguiente inicio de sesión.
    /// </summary>
    /// <remarks>
    /// Es idempotente: solo toca a quien todavía tiene un login distinto de
    /// su teléfono. Así, una vez convertido, nunca se le vuelve a pisar la
    /// contraseña que el usuario haya elegido. Se omite a quien no tenga
    /// teléfono válido o cuyo teléfono ya lo use otra persona, para no
    /// romper el índice único de logins.
    /// </remarks>
    /// <returns>Número de usuarios actualizados.</returns>
    public static async Task<int> SincronizarLoginsUsuariosAsync(
        AppDbContext db,
        IHasherContrasena hasher,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(hasher);

        var usuarios = await db.Usuarios
            .Where(u => u.Rol == Rol.Usuario)
            .ToListAsync(ct);

        var pendientes = usuarios
            .Where(u => !string.Equals(u.Login, u.Telefono, StringComparison.Ordinal))
            .ToList();

        if (pendientes.Count == 0)
        {
            return 0;
        }

        // Logins ya ocupados por cualquier otro usuario (incluidos admin y escaparate).
        var ocupados = await db.Usuarios
            .Select(u => new { u.Id, u.Login })
            .ToListAsync(ct);

        var actualizados = 0;

        foreach (var usuario in pendientes)
        {
            var telefono = Texto.NormalizarTelefono(usuario.Telefono);

            // Sin teléfono utilizable no hay login posible: se deja como estaba.
            if (telefono.Length < 6 || telefono.Length > 50)
            {
                continue;
            }

            var colision = ocupados.Any(o =>
                o.Id != usuario.Id &&
                string.Equals(o.Login, telefono, StringComparison.OrdinalIgnoreCase));

            if (colision)
            {
                continue;
            }

            usuario.Telefono = telefono;
            usuario.Login = telefono;
            usuario.PasswordHash = hasher.Hashear(telefono);
            usuario.DebeCambiarPassword = true;

            // Mantiene la lista de ocupados al día para los siguientes del bucle.
            ocupados.RemoveAll(o => o.Id == usuario.Id);
            ocupados.Add(new { usuario.Id, Login = telefono });

            actualizados++;
        }

        if (actualizados > 0)
        {
            await db.SaveChangesAsync(ct);
        }

        return actualizados;
    }
}
