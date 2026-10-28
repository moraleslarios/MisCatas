using Microsoft.AspNetCore.Identity;

namespace MisCatas.Application.Seguridad;

/// <summary>
/// Implementación de <see cref="IHasherContrasena"/> sobre
/// <see cref="PasswordHasher{TUser}"/> de ASP.NET Core Identity, que usa
/// PBKDF2 con HMAC-SHA512 y 100.000 iteraciones (formato v3).
/// No usamos todo Identity, solo su hasher, que está probado y es seguro.
/// </summary>
public class HasherContrasenaIdentity : IHasherContrasena
{
    // El tipo genérico es irrelevante: el hasher no inspecciona el usuario.
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object UsuarioFicticio = new();

    public string Hashear(string contrasenaEnClaro)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contrasenaEnClaro);
        return _hasher.HashPassword(UsuarioFicticio, contrasenaEnClaro);
    }

    public ResultadoVerificacion Verificar(string hashAlmacenado, string contrasenaEnClaro)
    {
        if (string.IsNullOrWhiteSpace(hashAlmacenado) || string.IsNullOrEmpty(contrasenaEnClaro))
        {
            return ResultadoVerificacion.Fallida;
        }

        var resultado = _hasher.VerifyHashedPassword(
            UsuarioFicticio,
            hashAlmacenado,
            contrasenaEnClaro);

        return resultado switch
        {
            PasswordVerificationResult.Success => ResultadoVerificacion.Correcta,
            PasswordVerificationResult.SuccessRehashNeeded => ResultadoVerificacion.CorrectaNecesitaRehash,
            _ => ResultadoVerificacion.Fallida
        };
    }
}
