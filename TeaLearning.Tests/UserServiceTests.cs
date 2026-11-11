using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Services.DTOs.Users;
using TeaLearning.Services.Interfaces;
using TeaLearning.Services.Services;

namespace TeaLearning.Tests;

/// <summary>Almacenamiento configurable para cubrir rutas de error del avatar.</summary>
internal sealed class ConfigurableFileStorage : IFileStorageService
{
    public bool ThrowArgument { get; set; }
    public List<string> Deleted { get; } = new();

    public Task<string> SaveAvatarAsync(Guid userId, Stream fileStream, string originalFileName, string contentType, CancellationToken cancellationToken = default)
    {
        if (ThrowArgument) throw new ArgumentException("Formato no permitido.");
        return Task.FromResult($"/uploads/avatars/{userId}.png");
    }

    public Task<bool> DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        Deleted.Add(relativePath);
        return Task.FromResult(true);
    }
}

public class UserServiceTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private IUserService Users => _host.Get<IUserService>();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task GetAllUsers_DevuelveRolesPerfilesYEstudiantesActivosDelAcudiente()
    {
        var (_, guardian) = await _host.CreateGuardianAsync();
        var (_, active) = await _host.CreateStudentAsync(guardian: guardian);
        var (_, inactive) = await _host.CreateStudentAsync(guardian: guardian);
        inactive.IsActive = false;
        await _host.Context.SaveChangesAsync();
        await _host.CreateTeacherAsync();

        var result = await Users.GetAllUsersAsync();

        Assert.True(result.Success);
        Assert.Equal(4, result.Data!.Count);
        var guardianDto = result.Data.Single(u => u.AcudienteId == guardian.Id);
        Assert.Single(guardianDto.StudentUserIds);
        Assert.Equal(active.UserId, guardianDto.StudentUserIds[0]);
        Assert.Contains(result.Data, u => u.DocenteId != null);
    }

    [Fact]
    public async Task GetUserById_Existente_DevuelveDto()
    {
        var (user, _) = await _host.CreateGuardianAsync();
        user.UltimaActividadAt = DateTime.UtcNow;
        await _host.UserManager.UpdateAsync(user);

        var result = await Users.GetUserByIdAsync(user.Id);

        Assert.True(result.Success);
        Assert.Contains("Acudiente", result.Data!.Roles);
        Assert.NotNull(result.Data.UltimaActividadAt);
    }

    [Fact]
    public async Task GetUserById_Inexistente_Falla()
    {
        var result = await Users.GetUserByIdAsync(Guid.NewGuid());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task GetCurrentUserProfile_DelegaEnGetUserById()
    {
        var admin = await _host.CreateAdminAsync();

        var result = await Users.GetCurrentUserProfileAsync(admin.Id);

        Assert.True(result.Success);
        Assert.Equal(admin.Id, result.Data!.Id);
    }

    // ------------------------------------------------------------ roles

    [Fact]
    public async Task UpdateUserRoles_UsuarioInexistente_Falla()
    {
        var result = await Users.UpdateUserRolesAsync(Guid.NewGuid(), new UpdateUserRolesDto { Roles = new() { "Docente" } });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateUserRoles_SinRoles_Falla()
    {
        var admin = await _host.CreateAdminAsync();

        var result = await Users.UpdateUserRolesAsync(admin.Id, new UpdateUserRolesDto { Roles = new() });

        Assert.False(result.Success);
        Assert.Contains("al menos un rol", result.Message);
    }

    [Fact]
    public async Task UpdateUserRoles_RolInexistente_Falla()
    {
        var admin = await _host.CreateAdminAsync();

        var result = await Users.UpdateUserRolesAsync(admin.Id, new UpdateUserRolesDto { Roles = new() { "Fantasma" } });

        Assert.False(result.Success);
        Assert.Contains("no existe", result.Message);
    }

    [Theory]
    [InlineData("Docente")]
    [InlineData("Acudiente")]
    [InlineData("Estudiante")]
    public async Task UpdateUserRoles_CreaPerfilDelNuevoRolYRevocaSesion(string newRole)
    {
        await _host.CreateClassroomAsync();
        var user = await _host.CreateUserAsync("rol@test.com", "Administrador");
        await _host.CreateUserAsync("x@test.com", newRole); // asegura que el rol exista
        user.RefreshToken = "abc";
        await _host.UserManager.UpdateAsync(user);

        var result = await Users.UpdateUserRolesAsync(user.Id, new UpdateUserRolesDto { Roles = new() { newRole } });

        Assert.True(result.Success);
        Assert.Equal(new[] { newRole }, result.Data!.Roles);
        var stored = await _host.UserManager.FindByIdAsync(user.Id.ToString());
        Assert.Null(stored!.RefreshToken);
        Assert.Equal(1, newRole switch
        {
            "Docente" => await _host.Context.TeacherProfiles.CountAsync(t => t.UserId == user.Id),
            "Acudiente" => await _host.Context.GuardianProfiles.CountAsync(g => g.UserId == user.Id),
            _ => await _host.Context.StudentProfiles.CountAsync(s => s.UserId == user.Id)
        });
    }

    [Fact]
    public async Task UpdateUserRoles_ReactivaPerfilesInactivosExistentes()
    {
        var (tUser, teacher) = await _host.CreateTeacherAsync();
        var (gUser, guardian) = await _host.CreateGuardianAsync();
        var (sUser, student) = await _host.CreateStudentAsync();
        teacher.IsActive = guardian.IsActive = student.IsActive = false;
        await _host.Context.SaveChangesAsync();

        await Users.UpdateUserRolesAsync(tUser.Id, new UpdateUserRolesDto { Roles = new() { "Docente" } });
        await Users.UpdateUserRolesAsync(gUser.Id, new UpdateUserRolesDto { Roles = new() { "Acudiente" } });
        await Users.UpdateUserRolesAsync(sUser.Id, new UpdateUserRolesDto { Roles = new() { "Estudiante" } });

        Assert.True((await _host.Context.TeacherProfiles.AsNoTracking().SingleAsync(t => t.Id == teacher.Id)).IsActive);
        Assert.True((await _host.Context.GuardianProfiles.AsNoTracking().SingleAsync(g => g.Id == guardian.Id)).IsActive);
        Assert.True((await _host.Context.StudentProfiles.AsNoTracking().SingleAsync(s => s.Id == student.Id)).IsActive);
    }

    // ------------------------------------------------------------ actualización

    [Fact]
    public async Task UpdateUser_Inexistente_Falla()
    {
        var result = await Users.UpdateUserAsync(Guid.NewGuid(), new UpdateUserDto());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateUser_ActualizaNombresApellidosYCorreo()
    {
        var user = await _host.CreateUserAsync("viejo@test.com", "Docente");

        var result = await Users.UpdateUserAsync(user.Id, new UpdateUserDto
        {
            Nombres = " Nuevo ",
            Apellidos = " Apellido Nuevo ",
            Email = " NUEVO@Test.com "
        });

        Assert.True(result.Success);
        Assert.Equal("Nuevo", result.Data!.Nombres);
        Assert.Equal("Apellido Nuevo", result.Data.Apellidos);
        Assert.Equal("nuevo@test.com", result.Data.Email);
        Assert.NotNull(await _host.UserManager.FindByEmailAsync("nuevo@test.com"));
    }

    [Fact]
    public async Task UpdateUser_MismoCorreo_NoCambiaNada()
    {
        var user = await _host.CreateUserAsync("igual@test.com", "Docente");

        var result = await Users.UpdateUserAsync(user.Id, new UpdateUserDto { Email = "IGUAL@test.com" });

        Assert.True(result.Success);
        Assert.Equal("igual@test.com", result.Data!.Email);
    }

    [Fact]
    public async Task UpdateUser_CorreoDeOtroUsuario_Falla()
    {
        await _host.CreateUserAsync("ocupado@test.com", "Docente");
        var user = await _host.CreateUserAsync("libre@test.com", "Docente");

        var result = await Users.UpdateUserAsync(user.Id, new UpdateUserDto { Email = "ocupado@test.com" });

        Assert.False(result.Success);
        Assert.Contains("Ya existe", result.Message);
    }

    [Fact]
    public async Task UpdateUser_Desactivar_RevocaSesion()
    {
        var user = await _host.CreateUserAsync("baja@test.com", "Docente");
        user.RefreshToken = "tok";
        await _host.UserManager.UpdateAsync(user);

        var result = await Users.UpdateUserAsync(user.Id, new UpdateUserDto { IsActive = false });

        Assert.True(result.Success);
        Assert.False(result.Data!.IsActive);
        Assert.Null((await _host.UserManager.FindByIdAsync(user.Id.ToString()))!.RefreshToken);
    }

    [Fact]
    public async Task UpdateUser_Reactivar_NoRevocaSesion()
    {
        var user = await _host.CreateUserAsync("alta@test.com", "Docente", isActive: false);

        var result = await Users.UpdateUserAsync(user.Id, new UpdateUserDto { IsActive = true });

        Assert.True(result.Success);
        Assert.True(result.Data!.IsActive);
    }

    [Fact]
    public async Task UpdateUser_ConRoles_ReemplazaRolesYCreaPerfil()
    {
        var user = await _host.CreateUserAsync("cambio@test.com", "Administrador");
        await _host.CreateUserAsync("otro@test.com", "Docente");

        var result = await Users.UpdateUserAsync(user.Id, new UpdateUserDto { Roles = new() { "Docente" } });

        Assert.True(result.Success);
        Assert.Equal(new[] { "Docente" }, result.Data!.Roles);
        Assert.NotNull(result.Data.DocenteId);
    }

    [Fact]
    public async Task UpdateUser_ConRolInexistente_Falla()
    {
        var user = await _host.CreateUserAsync("rolx@test.com", "Docente");

        var result = await Users.UpdateUserAsync(user.Id, new UpdateUserDto { Roles = new() { "Fantasma" } });

        Assert.False(result.Success);
        Assert.Contains("no existe", result.Message);
    }

    // ------------------------------------------------------------ eliminación

    [Fact]
    public async Task DeleteUser_Inexistente_Falla()
    {
        var result = await Users.DeleteUserAsync(Guid.NewGuid());

        Assert.False(result.Success);
    }

    [Fact]
    public async Task DeleteUser_Administrador_Elimina()
    {
        var admin = await _host.CreateAdminAsync();

        var result = await Users.DeleteUserAsync(admin.Id);

        Assert.True(result.Success);
        Assert.Null(await _host.UserManager.FindByIdAsync(admin.Id.ToString()));
    }

    [Fact]
    public async Task DeleteUser_EstudianteSinHistorial_Elimina()
    {
        var (user, _) = await _host.CreateStudentAsync();

        var result = await Users.DeleteUserAsync(user.Id);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task DeleteUser_EstudianteConEjecuciones_NoPermite()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var (user, student) = await _host.CreateStudentAsync();
        var module = await _host.CreateModuleAsync();
        var activity = await _host.CreateActivityAsync(module, teacher);
        var assignment = await _host.CreateAssignmentAsync(activity, student);
        _host.Context.ActivityExecutions.Add(new ActivityExecution { AssignmentId = assignment.Id, StudentProfileId = student.Id });
        await _host.Context.SaveChangesAsync();

        var result = await Users.DeleteUserAsync(user.Id);

        Assert.False(result.Success);
        Assert.Contains("historial", result.Message);
    }

    [Fact]
    public async Task DeleteUser_EstudianteConNotas_NoPermite()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var (user, student) = await _host.CreateStudentAsync();
        _host.Context.PedagogicalNotes.Add(new PedagogicalNote { StudentProfileId = student.Id, TeacherProfileId = teacher.Id, Contenido = "n" });
        await _host.Context.SaveChangesAsync();

        Assert.False((await Users.DeleteUserAsync(user.Id)).Success);
    }

    [Fact]
    public async Task DeleteUser_EstudianteConCalificaciones_NoPermite()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var (user, student) = await _host.CreateStudentAsync();
        var activity = await _host.CreateActivityAsync(await _host.CreateModuleAsync(), teacher);
        var assignment = await _host.CreateAssignmentAsync(activity, student);
        assignment.Calificacion = 4m;
        await _host.Context.SaveChangesAsync();

        Assert.False((await Users.DeleteUserAsync(user.Id)).Success);
    }

    [Fact]
    public async Task DeleteUser_DocenteConActividades_NoPermite()
    {
        var (user, teacher) = await _host.CreateTeacherAsync();
        await _host.CreateActivityAsync(await _host.CreateModuleAsync(), teacher);

        var result = await Users.DeleteUserAsync(user.Id);

        Assert.False(result.Success);
        Assert.Contains("docente", result.Message);
    }

    [Fact]
    public async Task DeleteUser_DocenteSinVinculos_Elimina()
    {
        var (user, _) = await _host.CreateTeacherAsync();

        Assert.True((await Users.DeleteUserAsync(user.Id)).Success);
    }

    [Fact]
    public async Task DeleteUser_AcudienteDesvinculaEstudiantes()
    {
        var (user, guardian) = await _host.CreateGuardianAsync();
        var (_, student) = await _host.CreateStudentAsync(guardian: guardian);

        var result = await Users.DeleteUserAsync(user.Id);

        Assert.True(result.Success);
        Assert.Null((await _host.Context.StudentProfiles.AsNoTracking().SingleAsync(s => s.Id == student.Id)).GuardianProfileId);
    }

    // ------------------------------------------------------------ vínculos acudiente

    [Fact]
    public async Task LinkStudents_UsuarioInexistente_Falla()
    {
        Assert.False((await Users.LinkStudentsToGuardianAsync(Guid.NewGuid(), new List<Guid>())).Success);
    }

    [Fact]
    public async Task LinkStudents_SinRolAcudiente_Falla()
    {
        var teacher = await _host.CreateUserAsync("d@test.com", "Docente");

        var result = await Users.LinkStudentsToGuardianAsync(teacher.Id, new List<Guid>());

        Assert.False(result.Success);
        Assert.Contains("Acudiente", result.Message);
    }

    [Fact]
    public async Task LinkStudents_CreaPerfilSiNoExisteYVinculaPorIdOUsuario()
    {
        var guardianUser = await _host.CreateUserAsync("g@test.com", "Acudiente");
        var (_, s1) = await _host.CreateStudentAsync();
        var (_, s2) = await _host.CreateStudentAsync();

        var result = await Users.LinkStudentsToGuardianAsync(guardianUser.Id, new List<Guid> { s1.Id, s2.UserId });

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
        var guardian = await _host.Context.GuardianProfiles.SingleAsync(g => g.UserId == guardianUser.Id);
        Assert.All(_host.Context.StudentProfiles.ToList(), s => Assert.Equal(guardian.Id, s.GuardianProfileId));
    }

    [Fact]
    public async Task LinkStudents_DesvinculaLosQueNoEstanEnLaListaNueva()
    {
        var (guardianUser, guardian) = await _host.CreateGuardianAsync();
        var (_, keep) = await _host.CreateStudentAsync(guardian: guardian);
        var (_, drop) = await _host.CreateStudentAsync(guardian: guardian);

        var result = await Users.LinkStudentsToGuardianAsync(guardianUser.Id, new List<Guid> { keep.Id });

        Assert.True(result.Success);
        Assert.Equal(guardian.Id, (await _host.Context.StudentProfiles.AsNoTracking().SingleAsync(s => s.Id == keep.Id)).GuardianProfileId);
        Assert.Null((await _host.Context.StudentProfiles.AsNoTracking().SingleAsync(s => s.Id == drop.Id)).GuardianProfileId);
    }

    [Fact]
    public async Task LinkStudents_ListaVacia_DesvinculaTodos()
    {
        var (guardianUser, guardian) = await _host.CreateGuardianAsync();
        await _host.CreateStudentAsync(guardian: guardian);

        var result = await Users.LinkStudentsToGuardianAsync(guardianUser.Id, new List<Guid>());

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
        Assert.All(_host.Context.StudentProfiles.ToList(), s => Assert.Null(s.GuardianProfileId));
    }

    // ------------------------------------------------------------ reset de contraseña de estudiante

    [Fact]
    public async Task ResetStudentPassword_EstudianteInexistente_Falla()
    {
        Assert.False((await Users.ResetStudentPasswordAsync(Guid.NewGuid(), Guid.NewGuid(), "Nueva123")).Success);
    }

    [Fact]
    public async Task ResetStudentPassword_AcudienteNoTitular_Falla()
    {
        var (_, guardian) = await _host.CreateGuardianAsync();
        var (otherUser, _) = await _host.CreateGuardianAsync();
        var (_, student) = await _host.CreateStudentAsync(guardian: guardian);

        var result = await Users.ResetStudentPasswordAsync(otherUser.Id, student.Id, "Nueva123");

        Assert.False(result.Success);
        Assert.Contains("autorización", result.Message);
    }

    [Fact]
    public async Task ResetStudentPassword_EstudianteSinAcudiente_FallaParaNoAdmin()
    {
        var (_, student) = await _host.CreateStudentAsync();

        Assert.False((await Users.ResetStudentPasswordAsync(Guid.NewGuid(), student.Id, "Nueva123")).Success);
    }

    [Fact]
    public async Task ResetStudentPassword_AcudienteTitular_CambiaContrasenaYCierraSesiones()
    {
        var (guardianUser, guardian) = await _host.CreateGuardianAsync();
        var (studentUser, student) = await _host.CreateStudentAsync(guardian: guardian);
        studentUser.RefreshToken = "vigente";
        await _host.UserManager.UpdateAsync(studentUser);

        var result = await Users.ResetStudentPasswordAsync(guardianUser.Id, student.UserId, "NuevaClave1");

        Assert.True(result.Success);
        var stored = await _host.UserManager.FindByIdAsync(studentUser.Id.ToString());
        Assert.True(await _host.UserManager.CheckPasswordAsync(stored!, "NuevaClave1"));
        Assert.Null(stored!.RefreshToken);
    }

    [Fact]
    public async Task ResetStudentPassword_AdminPuedeSinSerTitular()
    {
        var (_, student) = await _host.CreateStudentAsync();

        var result = await Users.ResetStudentPasswordAsync(Guid.NewGuid(), student.Id, "NuevaClave1", isAdmin: true);

        Assert.True(result.Success);
    }

    [Fact]
    public async Task ResetStudentPassword_ContrasenaInvalida_DevuelveErroresTraducidos()
    {
        var (_, student) = await _host.CreateStudentAsync();

        var result = await Users.ResetStudentPasswordAsync(Guid.NewGuid(), student.Id, "abc", isAdmin: true);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Errors);
    }

    // ------------------------------------------------------------ avatar

    private UserService CreateWithStorage(ConfigurableFileStorage storage)
        => new(_host.UserManager, _host.Get<RoleManager<IdentityRole<Guid>>>(), _host.Context, storage);

    [Fact]
    public async Task UpdateAvatar_UsuarioInexistente_Falla()
    {
        var svc = CreateWithStorage(new ConfigurableFileStorage());

        Assert.False((await svc.UpdateAvatarAsync(Guid.NewGuid(), new MemoryStream(), "a.png", "image/png")).Success);
    }

    [Fact]
    public async Task UpdateAvatar_ReemplazaAvatarPrevioYGuardaNuevo()
    {
        var storage = new ConfigurableFileStorage();
        var svc = CreateWithStorage(storage);
        var user = await _host.CreateAdminAsync();
        user.AvatarUrl = "/uploads/avatars/viejo.png";
        await _host.UserManager.UpdateAsync(user);

        var result = await svc.UpdateAvatarAsync(user.Id, new MemoryStream(new byte[] { 1 }), "a.png", "image/png");

        Assert.True(result.Success);
        Assert.Contains("/uploads/avatars/viejo.png", storage.Deleted);
        Assert.EndsWith(".png", result.Data!.AvatarUrl);
    }

    [Fact]
    public async Task UpdateAvatar_ArchivoNoPermitido_DevuelveMensajeDelAlmacenamiento()
    {
        var svc = CreateWithStorage(new ConfigurableFileStorage { ThrowArgument = true });
        var user = await _host.CreateAdminAsync();

        var result = await svc.UpdateAvatarAsync(user.Id, new MemoryStream(), "a.exe", "application/x");

        Assert.False(result.Success);
        Assert.Equal("Formato no permitido.", result.Message);
    }

    [Fact]
    public async Task RemoveAvatar_Inexistente_Falla()
    {
        Assert.False((await Users.RemoveAvatarAsync(Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task RemoveAvatar_ConAvatar_LoBorra()
    {
        var storage = new ConfigurableFileStorage();
        var svc = CreateWithStorage(storage);
        var user = await _host.CreateAdminAsync();
        user.AvatarUrl = "/uploads/avatars/x.png";
        await _host.UserManager.UpdateAsync(user);

        var result = await svc.RemoveAvatarAsync(user.Id);

        Assert.True(result.Success);
        Assert.Single(storage.Deleted);
        Assert.Null((await _host.UserManager.FindByIdAsync(user.Id.ToString()))!.AvatarUrl);
    }

    [Fact]
    public async Task RemoveAvatar_SinAvatar_NoHaceNada()
    {
        var storage = new ConfigurableFileStorage();
        var svc = CreateWithStorage(storage);
        var user = await _host.CreateAdminAsync();

        var result = await svc.RemoveAvatarAsync(user.Id);

        Assert.True(result.Success);
        Assert.Empty(storage.Deleted);
    }
}
