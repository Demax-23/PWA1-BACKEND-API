namespace TeaLearning.Tests;

public class GuardianStudentPasswordResetTests
{
    [Fact]
    public async Task ResetStudentPassword_ByTitularGuardian_ChangesPasswordAndRevokesRefreshToken()
    {
        using var host = new PasswordTestHost();
        var (guardian, student, studentUser) = await host.CreateGuardianWithStudentAsync(
            "tealearning.rol+acudiente@gmail.com", "estudiante@tealearning.com", "Estudiante123!*");

        var result = await host.Users.ResetStudentPasswordAsync(guardian.UserId, student.Id, "Nueva456");

        Assert.True(result.Success);
        var reloaded = await host.UserManager.FindByIdAsync(studentUser.Id.ToString());
        Assert.True(await host.UserManager.CheckPasswordAsync(reloaded!, "Nueva456"));
        Assert.False(await host.UserManager.CheckPasswordAsync(reloaded!, "Estudiante123!*"));
        Assert.Null(reloaded!.RefreshToken);
        Assert.Empty(host.Email.Sent);   // el estudiante no tiene correo real: no se envía nada
    }

    [Fact]
    public async Task ResetStudentPassword_ByUserIdOfStudent_AlsoWorks()
    {
        using var host = new PasswordTestHost();
        var (guardian, _, studentUser) = await host.CreateGuardianWithStudentAsync(
            "tealearning.rol+acudiente@gmail.com", "estudiante@tealearning.com", "Estudiante123!*");

        var result = await host.Users.ResetStudentPasswordAsync(guardian.UserId, studentUser.Id, "Nueva456");

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ResetStudentPassword_ByAnotherGuardian_IsForbiddenAndKeepsPassword()
    {
        using var host = new PasswordTestHost();
        var (_, student, studentUser) = await host.CreateGuardianWithStudentAsync(
            "tealearning.rol+acudiente@gmail.com", "estudiante@tealearning.com", "Estudiante123!*");
        var (otherGuardian, _, _) = await host.CreateGuardianWithStudentAsync(
            "otro.acudiente@gmail.com", "otro.estudiante@tealearning.com", "Otro123!*");

        var result = await host.Users.ResetStudentPasswordAsync(otherGuardian.UserId, student.Id, "Hackeada1");

        Assert.False(result.Success);
        Assert.Contains("autorización", result.Message, StringComparison.OrdinalIgnoreCase);
        var reloaded = await host.UserManager.FindByIdAsync(studentUser.Id.ToString());
        Assert.True(await host.UserManager.CheckPasswordAsync(reloaded!, "Estudiante123!*"));
    }

    [Fact]
    public async Task ResetStudentPassword_ByAdministrator_IsNotAllowed()
    {
        using var host = new PasswordTestHost();
        var (_, student, studentUser) = await host.CreateGuardianWithStudentAsync(
            "tealearning.rol+acudiente@gmail.com", "estudiante@tealearning.com", "Estudiante123!*");
        var admin = await host.CreateUserAsync("tealearning.rol+administrador@gmail.com", "Admin123!*", "Administrador");

        var result = await host.Users.ResetStudentPasswordAsync(admin.Id, student.Id, "Nueva456");

        Assert.False(result.Success);
        Assert.Contains("autorización", result.Message, StringComparison.OrdinalIgnoreCase);
        var reloaded = await host.UserManager.FindByIdAsync(studentUser.Id.ToString());
        Assert.True(await host.UserManager.CheckPasswordAsync(reloaded!, "Estudiante123!*"));
    }

    [Fact]
    public async Task ResetStudentPassword_ForStudentWithoutGuardian_IsForbidden()
    {
        using var host = new PasswordTestHost();
        var guardianUser = await host.CreateUserAsync("tealearning.rol+acudiente@gmail.com", "Acudiente123!*", "Acudiente");
        var studentUser = await host.CreateUserAsync("huerfano@tealearning.com", "Estudiante123!*", "Estudiante");
        var student = new TeaLearning.Core.Entities.StudentProfile
        {
            UserId = studentUser.Id,
            CodigoEstudiante = "EST-0001",
            FechaNacimiento = new DateTime(2017, 1, 1)
        };
        host.Context.StudentProfiles.Add(student);
        await host.Context.SaveChangesAsync();

        var result = await host.Users.ResetStudentPasswordAsync(guardianUser.Id, student.Id, "Nueva456");

        Assert.False(result.Success);
        Assert.Contains("autorización", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetStudentPassword_UnknownStudent_ReturnsNotFound()
    {
        using var host = new PasswordTestHost();
        var guardianUser = await host.CreateUserAsync("tealearning.rol+acudiente@gmail.com", "Acudiente123!*", "Acudiente");

        var result = await host.Users.ResetStudentPasswordAsync(guardianUser.Id, Guid.NewGuid(), "Nueva456");

        Assert.False(result.Success);
        Assert.Contains("no encontrado", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("ab1", "al menos 6")]
    [InlineData("sinnumero", "un número")]
    public async Task ResetStudentPassword_WithPasswordBelowPolicy_FailsInSpanish(string password, string expectedFragment)
    {
        using var host = new PasswordTestHost();
        var (guardian, student, studentUser) = await host.CreateGuardianWithStudentAsync(
            "tealearning.rol+acudiente@gmail.com", "estudiante@tealearning.com", "Estudiante123!*");

        var result = await host.Users.ResetStudentPasswordAsync(guardian.UserId, student.Id, password);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains(expectedFragment));
        var reloaded = await host.UserManager.FindByIdAsync(studentUser.Id.ToString());
        Assert.True(await host.UserManager.CheckPasswordAsync(reloaded!, "Estudiante123!*"));
    }
}
