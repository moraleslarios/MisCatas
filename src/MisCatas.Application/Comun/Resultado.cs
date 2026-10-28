namespace MisCatas.Application.Comun;

/// <summary>
/// Resultado de una operación de negocio. Evita usar excepciones para
/// comunicar errores previsibles (login duplicado, validación, etc.).
/// </summary>
public class Resultado
{
    protected Resultado(bool exito, string? error)
    {
        Exito = exito;
        Error = error;
    }

    /// <summary>Indica si la operación terminó correctamente.</summary>
    public bool Exito { get; }

    /// <summary>Mensaje de error en español listo para mostrar al usuario.</summary>
    public string? Error { get; }

    /// <summary>Indica si la operación falló.</summary>
    public bool EsFallo => !Exito;

    public static Resultado Ok() => new(true, null);

    public static Resultado Fallo(string error) => new(false, error);

    public static Resultado<T> Ok<T>(T valor) => Resultado<T>.Correcto(valor);

    public static Resultado<T> Fallo<T>(string error) => Resultado<T>.Incorrecto(error);
}

/// <summary>Resultado de una operación que además devuelve un valor.</summary>
/// <typeparam name="T">Tipo del valor devuelto.</typeparam>
public class Resultado<T> : Resultado
{
    private Resultado(bool exito, T? valor, string? error) : base(exito, error)
    {
        Valor = valor;
    }

    /// <summary>Valor devuelto. Solo es válido cuando <see cref="Resultado.Exito"/> es <c>true</c>.</summary>
    public T? Valor { get; }

    internal static Resultado<T> Correcto(T valor) => new(true, valor, null);

    internal static Resultado<T> Incorrecto(string error) => new(false, default, error);
}
