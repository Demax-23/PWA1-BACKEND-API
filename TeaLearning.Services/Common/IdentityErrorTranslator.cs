using Microsoft.AspNetCore.Identity;

namespace TeaLearning.Services.Common;

/// <summary>
/// Traduce los errores de Identity (que vienen en inglés) a mensajes en español para el usuario final.
/// </summary>
public static class IdentityErrorTranslator
{
    public const string InvalidTokenCode = "InvalidToken";

    public static List<string> Translate(IEnumerable<IdentityError> errors)
    {
        return errors.Select(e => e.Code switch
        {
            "PasswordTooShort" => "La contraseña debe tener al menos 6 caracteres.",
            "PasswordRequiresDigit" => "La contraseña debe incluir al menos un número.",
            "PasswordRequiresLower" => "La contraseña debe incluir al menos una letra minúscula.",
            "PasswordRequiresUpper" => "La contraseña debe incluir al menos una letra mayúscula.",
            "PasswordRequiresNonAlphanumeric" => "La contraseña debe incluir al menos un símbolo.",
            "PasswordRequiresUniqueChars" => "La contraseña debe incluir más caracteres distintos.",
            _ => e.Description
        }).Distinct().ToList();
    }
}
