using System.Globalization;
using System.Text;

namespace MisCatas.Application.Comun;

/// <summary>Utilidades de normalización de texto.</summary>
public static class Texto
{
    /// <summary>
    /// Quita los acentos y diacríticos de una cadena
    /// (por ejemplo «Ramírez» pasa a «Ramirez»).
    /// </summary>
    public static string QuitarAcentos(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var descompuesto = valor.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            // La 'ñ' se descompone en 'n' + tilde; aquí se conserva solo la 'n',
            // que es lo deseable para construir un login.
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                resultado.Append(caracter);
            }
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Genera la base de un login a partir del nombre y los apellidos:
    /// nombre + primer apellido, en minúsculas, sin acentos ni caracteres
    /// que no sean letras o dígitos.
    /// </summary>
    public static string GenerarBaseLogin(string? nombre, string? apellidos)
    {
        var primerApellido = (apellidos ?? string.Empty)
            .Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;

        var bruto = QuitarAcentos($"{nombre}{primerApellido}").ToLowerInvariant();

        var limpio = new StringBuilder(bruto.Length);
        foreach (var caracter in bruto)
        {
            if (char.IsLetterOrDigit(caracter))
            {
                limpio.Append(caracter);
            }
        }

        return limpio.ToString();
    }

    /// <summary>
    /// Normaliza un teléfono dejando solo dígitos y un posible '+' inicial.
    /// </summary>
    public static string NormalizarTelefono(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono))
        {
            return string.Empty;
        }

        var recortado = telefono.Trim();
        var empiezaConMas = recortado.StartsWith('+');

        var digitos = new StringBuilder(recortado.Length);
        foreach (var caracter in recortado)
        {
            if (char.IsDigit(caracter))
            {
                digitos.Append(caracter);
            }
        }

        return empiezaConMas ? $"+{digitos}" : digitos.ToString();
    }
}
