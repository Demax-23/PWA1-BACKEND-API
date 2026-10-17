using System.Text.RegularExpressions;
using TeaLearning.Services.DTOs.Auth;

namespace TeaLearning.Tests;

public class PasswordResetServiceTests
{
    private const string AdminEmail = "tealearning.rol+administrador@gmail.com";
    private const string TeacherEmail = "tealearning.rol+docente@gmail.com";
    private const string GuardianEmail = "tealearning.rol+acudiente@gmail.com";

    /// <summary>Extrae email y token (Base64Url) del enlace del correo, tal como lo haría reset-password.html.</summary>
    private static (string Email, string Token) ParseLink(string textBody)
    {
        var match = Regex.Match(textBody, @"reset-password\.html#email=(?<email>[^&\s]+)&token=(?<token>[^\s]+)");
        Assert.True(match.Success, "El correo no contiene el enlace de restablecimiento.");
        return (Uri.UnescapeDataString(match.Groups["email"].Value), match.Groups["token"].Value);
    }

    private static ResetPasswordRequestDto ResetDto(string email, string token, string password) => new()
    {
        Email = email,
        Token = token,
        NewPassword = password,
        ConfirmPassword = password
    };

    [Theory]
    [InlineData(AdminEmail, "Administrador")]
    [InlineData(TeacherEmail, "Docente")]
    [InlineData(GuardianEmail, "Acudiente")]
    public async Task ForgotPassword_ForEmailRoles_SendsOneEmailWithFragmentLink(string email, string role)
    {
        using var host = new PasswordTestHost();
        await host.CreateUserAsync(email, "Clave123!*", role);

        var result = await host.PasswordReset.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = email });

        Assert.True(result.Success);
        var sent = Assert.Single(host.Email.Sent);
        Assert.Equal(email, sent.ToAddress);
        Assert.StartsWith(PasswordTestHost.FrontendBaseUrl + "/reset-password.html#", ExtractLinkStart(sent.TextBody));
        Assert.DoesNotContain("?token=", sent.TextBody);   // el token nunca va en el query string
        Assert.Contains("30 minutos", sent.TextBody);
    }

    private static string ExtractLinkStart(string textBody)
        => Regex.Match(textBody, @"http\S+").Value;

    [Fact]
    public async Task ForgotPassword_UnknownInactiveAndStudent_ReturnSameResponseAndSendNothing()
    {
        using var host = new PasswordTestHost();
        await host.CreateUserAsync("inactivo@gmail.com", "Clave123!*", "Docente", isActive: false);
        await host.CreateUserAsync("estudiante@tealearning.com", "Estudiante123!*", "Estudiante");
        await host.CreateUserAsync(TeacherEmail, "Clave123!*", "Docente");

        var known = await host.PasswordReset.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = TeacherEmail });
        host.Email.Sent.Clear();

        var unknown = await host.PasswordReset.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "nadie@gmail.com" });
        var inactive = await host.PasswordReset.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "inactivo@gmail.com" });
        var student = await host.PasswordReset.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = "estudiante@tealearning.com" });

        Assert.Empty(host.Email.Sent);
        foreach (var r in new[] { unknown, inactive, student })
        {
            Assert.True(r.Success);
            Assert.Equal(known.Message, r.Message);   // mensaje idéntico: no revela qué cuentas existen
        }
    }

    [Fact]
    public async Task ForgotPassword_WhenSmtpFails_StillReturnsGenericSuccess()
    {
        using var host = new PasswordTestHost();
        await host.CreateUserAsync(TeacherEmail, "Clave123!*", "Docente");
        host.Email.ShouldFail = true;

        var result = await host.PasswordReset.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = TeacherEmail });

        Assert.True(result.Success);
        Assert.Empty(host.Email.Sent);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ChangesPasswordRevokesRefreshTokenAndSendsConfirmation()
    {
        using var host = new PasswordTestHost();
        var user = await host.CreateUserAsync(TeacherEmail, "Clave123!*", "Docente");
        await host.PasswordReset.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = TeacherEmail });
        var (email, token) = ParseLink(host.Email.Sent.Single().TextBody);
        host.Email.Sent.Clear();

        var result = await host.PasswordReset.ResetPasswordAsync(ResetDto(email, token, "Nueva456!*"));

        Assert.True(result.Success);
        var reloaded = await host.UserManager.FindByIdAsync(user.Id.ToString());
        Assert.True(await host.UserManager.CheckPasswordAsync(reloaded!, "Nueva456!*"));
        Assert.False(await host.UserManager.CheckPasswordAsync(reloaded!, "Clave123!*"));
        Assert.Null(reloaded!.RefreshToken);
        Assert.Null(reloaded.RefreshTokenExpiryTime);

        var confirmation = Assert.Single(host.Email.Sent);
        Assert.Contains("Tu contraseña fue modificada", confirmation.Subject);
    }

    [Fact]
    public async Task ResetPassword_ReusingToken_Fails()
    {
        using var host = new PasswordTestHost();
        await host.CreateUserAsync(TeacherEmail, "Clave123!*", "Docente");
        await host.PasswordReset.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = TeacherEmail });
        var (email, token) = ParseLink(host.Email.Sent.Single().TextBody);

        var first = await host.PasswordReset.ResetPasswordAsync(ResetDto(email, token, "Nueva456!*"));
        var second = await host.PasswordReset.ResetPasswordAsync(ResetDto(email, token, "Otra789!*"));

        Assert.True(first.Success);
        Assert.False(second.Success);
        var user = await host.UserManager.FindByEmailAsync(email);
        Assert.True(await host.UserManager.CheckPasswordAsync(user!, "Nueva456!*"));
    }

    [Fact]
    public async Task ResetPassword_WithTamperedOrForeignToken_Fails()
    {
        using var host = new PasswordTestHost();
        await host.CreateUserAsync(TeacherEmail, "Clave123!*", "Docente");
        await host.CreateUserAsync(AdminEmail, "Admin123!*", "Administrador");
        await host.PasswordReset.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = TeacherEmail });
        var (_, teacherToken) = ParseLink(host.Email.Sent.Single().TextBody);

        var tampered = await host.PasswordReset.ResetPasswordAsync(ResetDto(TeacherEmail, teacherToken[..^3] + "abc", "Nueva456!*"));
        var notBase64 = await host.PasswordReset.ResetPasswordAsync(ResetDto(TeacherEmail, "***", "Nueva456!*"));
        var foreign = await host.PasswordReset.ResetPasswordAsync(ResetDto(AdminEmail, teacherToken, "Nueva456!*"));

        Assert.False(tampered.Success);
        Assert.False(notBase64.Success);
        Assert.False(foreign.Success);
        var admin = await host.UserManager.FindByEmailAsync(AdminEmail);
        Assert.True(await host.UserManager.CheckPasswordAsync(admin!, "Admin123!*"));
    }

    [Fact]
    public async Task ResetPassword_WithWeakPassword_FailsInSpanishAndKeepsTokenUsable()
    {
        using var host = new PasswordTestHost();
        await host.CreateUserAsync(TeacherEmail, "Clave123!*", "Docente");
        await host.PasswordReset.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = TeacherEmail });
        var (email, token) = ParseLink(host.Email.Sent.Single().TextBody);

        var weak = await host.PasswordReset.ResetPasswordAsync(ResetDto(email, token, "abc"));

        Assert.False(weak.Success);
        Assert.Contains(weak.Errors, e => e.Contains("al menos 6 caracteres"));
        Assert.Contains(weak.Errors, e => e.Contains("un número"));

        // Una contraseña rechazada no consume el token: el usuario puede reintentar.
        var retry = await host.PasswordReset.ResetPasswordAsync(ResetDto(email, token, "Valida123"));
        Assert.True(retry.Success);
    }

    [Fact]
    public async Task ResetPassword_ForStudentAccount_IsRejected()
    {
        using var host = new PasswordTestHost();
        var student = await host.CreateUserAsync("estudiante@tealearning.com", "Estudiante123!*", "Estudiante");
        var rawToken = await host.UserManager.GeneratePasswordResetTokenAsync(student);
        var token = TeaLearning.Services.Common.Base64Url.Encode(rawToken);

        var result = await host.PasswordReset.ResetPasswordAsync(ResetDto(student.Email!, token, "Nueva456!*"));

        Assert.False(result.Success);
        var reloaded = await host.UserManager.FindByIdAsync(student.Id.ToString());
        Assert.True(await host.UserManager.CheckPasswordAsync(reloaded!, "Estudiante123!*"));
    }

    [Fact]
    public async Task ResetPassword_WhenConfirmationEmailFails_StillSucceeds()
    {
        using var host = new PasswordTestHost();
        await host.CreateUserAsync(TeacherEmail, "Clave123!*", "Docente");
        await host.PasswordReset.ForgotPasswordAsync(new ForgotPasswordRequestDto { Email = TeacherEmail });
        var (email, token) = ParseLink(host.Email.Sent.Single().TextBody);
        host.Email.ShouldFail = true;

        var result = await host.PasswordReset.ResetPasswordAsync(ResetDto(email, token, "Nueva456!*"));

        Assert.True(result.Success);
    }
}
