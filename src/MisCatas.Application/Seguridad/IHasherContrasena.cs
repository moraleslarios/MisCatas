namespace MisCatas.Application.Seguridad;

/// <summary>
/// Abstracción del hashing de contraseñas para poder sustituir el algoritmo
/// sin tocar los servicios de negocio.
/// </summary>
public interface IHasherContrasena
{
    /// <summary>Genera el hash de una contraseña en claro.</summary>
    string Hashear(string contrasenaEnClaro);

    /// <summary>
    /// Verifica una contraseña contra su hash.
    /// </summary>
    /// <param name="hashAlmacenado">Hash guardado en base de datos.</param>
    /// <param name="contrasenaEnClaro">Contraseña introducida por el usuario.</param>
    /// <returns>
    /// <see cref="ResultadoVerificacion"/> indicando si es válida y si conviene
    /// regenerar el hash porque usa parámetros antiguos.
    /// </returns>
    ResultadoVerificacion Verificar(string hashAlmacenado, string contrasenaEnClaro);
}

/// <summary>Resultado de verificar una contraseña.</summary>
public enum ResultadoVerificacion
{
    /// <summary>La contraseña no coincide.</summary>
    Fallida = 0,

    /// <summary>La contraseña coincide.</summary>
    Correcta = 1,

    /// <summary>La contraseña coincide pero el hash debería regenerarse.</summary>
    CorrectaNecesitaRehash = 2
}
