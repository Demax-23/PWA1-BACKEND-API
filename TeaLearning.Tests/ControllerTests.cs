using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using TeaLearning.Api.Controllers;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Activities;
using TeaLearning.Services.DTOs.Assignments;
using TeaLearning.Services.DTOs.Auth;
using TeaLearning.Services.DTOs.Categories;
using TeaLearning.Services.DTOs.Execution;
using TeaLearning.Services.DTOs.Guardian;
using TeaLearning.Services.DTOs.Modules;
using TeaLearning.Services.DTOs.Pedagogical;
using TeaLearning.Services.DTOs.Reports;
using TeaLearning.Services.DTOs.Sensory;
using TeaLearning.Services.DTOs.Users;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Tests;

internal static class Http
{
    public static int Code(IActionResult result) => result switch
    {
        ObjectResult o => o.StatusCode ?? 200,
        StatusCodeResult s => s.StatusCode,
        _ => throw new InvalidOperationException("Resultado inesperado: " + result.GetType().Name)
    };

    public static T Invalid<T>(T controller) where T : ControllerBase
    {
        controller.ModelState.AddModelError("campo", "valor inválido");
        return controller;
    }

    public static IFormFile File(long declaredLength, byte[]? content = null)
    {
        var bytes = content ?? new byte[] { 1, 2, 3 };
        return new FormFile(new MemoryStream(bytes), 0, declaredLength, "file", "avatar.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
    }
}

public class UsersControllerTests : IDisposable
{
    private readonly AppTestHost _host = new();
    public void Dispose() => _host.Dispose();

    private UsersController Ctl(Guid? user, params string[] roles)
        => AppTestHost.WithUser(new UsersController(_host.Get<IUserService>()), user, roles);

    [Fact]
    public async Task GetAll_Y_GetById()
    {
        var admin = await _host.CreateAdminAsync();
        var c = Ctl(admin.Id, "Administrador");

        Assert.Equal(200, Http.Code(await c.GetAll()));
        Assert.Equal(200, Http.Code(await c.GetById(admin.Id)));
        Assert.Equal(404, Http.Code(await c.GetById(Guid.NewGuid())));
    }

    [Fact]
    public async Task UpdateRoles_TodosLosCaminos()
    {
        var admin = await _host.CreateAdminAsync();
        var other = await _host.CreateUserAsync("o@test.com", "Estudiante");
        await _host.CreateUserAsync("d@test.com", "Docente");

        Assert.Equal(400, Http.Code(await Http.Invalid(Ctl(admin.Id, "Administrador")).UpdateRoles(other.Id, new UpdateUserRolesDto())));
        Assert.Equal(400, Http.Code(await Ctl(admin.Id, "Administrador").UpdateRoles(admin.Id, new UpdateUserRolesDto { Roles = new() { "Docente" } })));
        Assert.Equal(400, Http.Code(await Ctl(admin.Id, "Administrador").UpdateRoles(other.Id, new UpdateUserRolesDto { Roles = new() })));
        Assert.Equal(200, Http.Code(await Ctl(admin.Id, "Administrador").UpdateRoles(other.Id, new UpdateUserRolesDto { Roles = new() { "Docente" } })));
        Assert.Equal(200, Http.Code(await Ctl(admin.Id, "Administrador").UpdateRoles(admin.Id, new UpdateUserRolesDto { Roles = new() { "ADMINISTRADOR" } })));
    }

    [Fact]
    public async Task Update_TodosLosCaminos()
    {
        var admin = await _host.CreateAdminAsync();
        var other = await _host.CreateUserAsync("o@test.com", "Docente");

        Assert.Equal(400, Http.Code(await Http.Invalid(Ctl(admin.Id)).Update(other.Id, new UpdateUserDto())));
        Assert.Equal(400, Http.Code(await Ctl(admin.Id).Update(admin.Id, new UpdateUserDto { IsActive = false })));
        Assert.Equal(400, Http.Code(await Ctl(admin.Id).Update(admin.Id, new UpdateUserDto { Roles = new() { "Docente" } })));
        Assert.Equal(400, Http.Code(await Ctl(admin.Id).Update(Guid.NewGuid(), new UpdateUserDto())));
        Assert.Equal(200, Http.Code(await Ctl(admin.Id).Update(admin.Id, new UpdateUserDto { Nombres = "Nuevo", Roles = new() { "Administrador" } })));
        Assert.Equal(200, Http.Code(await Ctl(admin.Id).Update(other.Id, new UpdateUserDto { Apellidos = "X" })));
    }

    [Fact]
    public async Task Delete_TodosLosCaminos()
    {
        var admin = await _host.CreateAdminAsync();
        var other = await _host.CreateAdminAsync();

        Assert.Equal(400, Http.Code(await Ctl(admin.Id).Delete(admin.Id)));
        Assert.Equal(400, Http.Code(await Ctl(admin.Id).Delete(Guid.NewGuid())));
        Assert.Equal(200, Http.Code(await Ctl(admin.Id).Delete(other.Id)));
    }

    [Fact]
    public async Task LinkStudents_TodosLosCaminos()
    {
        var admin = await _host.CreateAdminAsync();
        var (gu, _) = await _host.CreateGuardianAsync();
        var (_, student) = await _host.CreateStudentAsync();

        Assert.Equal(400, Http.Code(await Http.Invalid(Ctl(admin.Id)).LinkStudents(gu.Id, new LinkStudentsDto())));
        Assert.Equal(400, Http.Code(await Ctl(admin.Id).LinkStudents(Guid.NewGuid(), new LinkStudentsDto())));
        Assert.Equal(200, Http.Code(await Ctl(admin.Id).LinkStudents(gu.Id, new LinkStudentsDto { StudentIds = new() { student.Id } })));
    }

    [Fact]
    public async Task Me_TodosLosCaminos()
    {
        var admin = await _host.CreateAdminAsync();

        Assert.Equal(401, Http.Code(await Ctl(null).GetCurrentUserProfile()));
        Assert.Equal(404, Http.Code(await Ctl(Guid.NewGuid()).GetCurrentUserProfile()));
        Assert.Equal(200, Http.Code(await Ctl(admin.Id).GetCurrentUserProfile()));
    }

    [Fact]
    public async Task UploadAvatar_TodosLosCaminos()
    {
        var admin = await _host.CreateAdminAsync();

        Assert.Equal(401, Http.Code(await Ctl(null).UploadAvatar(Http.File(3), CancellationToken.None)));
        Assert.Equal(400, Http.Code(await Ctl(admin.Id).UploadAvatar(null!, CancellationToken.None)));
        Assert.Equal(400, Http.Code(await Ctl(admin.Id).UploadAvatar(Http.File(0, Array.Empty<byte>()), CancellationToken.None)));
        Assert.Equal(400, Http.Code(await Ctl(admin.Id).UploadAvatar(Http.File(6 * 1024 * 1024), CancellationToken.None)));
        Assert.Equal(400, Http.Code(await Ctl(Guid.NewGuid()).UploadAvatar(Http.File(3), CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Ctl(admin.Id).UploadAvatar(Http.File(3), CancellationToken.None)));
    }

    [Fact]
    public async Task RemoveAvatar_TodosLosCaminos()
    {
        var admin = await _host.CreateAdminAsync();

        Assert.Equal(401, Http.Code(await Ctl(null).RemoveAvatar(CancellationToken.None)));
        Assert.Equal(400, Http.Code(await Ctl(Guid.NewGuid()).RemoveAvatar(CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Ctl(admin.Id).RemoveAvatar(CancellationToken.None)));
    }

    [Fact]
    public void BaseApiController_LeeIdsDeClaims()
    {
        var id = Guid.NewGuid();
        var student = Guid.NewGuid();
        var c = new ProbeController();
        c.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim("sub", id.ToString()),
                    new System.Security.Claims.Claim("estudianteId", student.ToString())
                }, "x"))
            }
        };

        Assert.Equal(id, c.UserId());
        Assert.Equal(student, c.StudentId());

        var anonymous = new ProbeController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        Assert.Null(anonymous.UserId());
        Assert.Null(anonymous.StudentId());
    }

    private sealed class ProbeController : BaseApiController
    {
        public Guid? UserId() => GetCurrentUserId();
        public Guid? StudentId() => GetCurrentStudentId();
    }
}

public class AuthControllerTests : IDisposable
{
    private readonly AppTestHost _host = new();
    public void Dispose() => _host.Dispose();

    private AuthController Ctl() => AppTestHost.WithUser(
        new AuthController(_host.Get<IAuthService>(), _host.Get<IPasswordResetService>()), null);

    [Fact]
    public async Task Register_TodosLosCaminos()
    {
        var dto = new RegisterRequestDto { Email = "n@test.com", Password = "Clave123!*", Nombres = "A", Apellidos = "B", Rol = "Docente" };

        Assert.Equal(400, Http.Code(await Http.Invalid(Ctl()).Register(dto)));
        Assert.Equal(200, Http.Code(await Ctl().Register(dto)));
        Assert.Equal(400, Http.Code(await Ctl().Register(dto))); // correo duplicado
    }

    [Fact]
    public async Task Login_TodosLosCaminos()
    {
        await _host.CreateUserAsync("l@test.com", "Docente");

        Assert.Equal(400, Http.Code(await Http.Invalid(Ctl()).Login(new LoginRequestDto())));
        Assert.Equal(401, Http.Code(await Ctl().Login(new LoginRequestDto { Email = "l@test.com", Password = "mala" })));
        Assert.Equal(200, Http.Code(await Ctl().Login(new LoginRequestDto { Email = "l@test.com", Password = AppTestHost.Password })));
    }

    [Fact]
    public async Task RefreshToken_TodosLosCaminos()
    {
        await _host.CreateUserAsync("r@test.com", "Docente");
        var login = await Ctl().Login(new LoginRequestDto { Email = "r@test.com", Password = AppTestHost.Password });
        var session = (ApiResponse<AuthResponseDto>)((OkObjectResult)login).Value!;

        Assert.Equal(400, Http.Code(await Http.Invalid(Ctl()).RefreshToken(new RefreshTokenRequestDto())));
        Assert.Equal(400, Http.Code(await Ctl().RefreshToken(new RefreshTokenRequestDto { AccessToken = "x", RefreshToken = "y" })));
        Assert.Equal(200, Http.Code(await Ctl().RefreshToken(new RefreshTokenRequestDto { AccessToken = session.Data!.Token, RefreshToken = session.Data.RefreshToken })));
    }

    [Fact]
    public async Task ForgotPassword_TodosLosCaminos()
    {
        await _host.CreateUserAsync("f@test.com", "Docente");

        Assert.Equal(400, Http.Code(await Http.Invalid(Ctl()).ForgotPassword(new ForgotPasswordRequestDto(), CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Ctl().ForgotPassword(new ForgotPasswordRequestDto { Email = "f@test.com" }, CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Ctl().ForgotPassword(new ForgotPasswordRequestDto { Email = "nadie@test.com" }, CancellationToken.None)));
    }

    [Fact]
    public async Task ResetPassword_TodosLosCaminos()
    {
        var user = await _host.CreateUserAsync("p@test.com", "Docente");
        var token = await _host.UserManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        Assert.Equal(400, Http.Code(await Http.Invalid(Ctl()).ResetPassword(new ResetPasswordRequestDto(), CancellationToken.None)));
        Assert.Equal(400, Http.Code(await Ctl().ResetPassword(new ResetPasswordRequestDto
        {
            Email = "p@test.com", Token = "invalido", NewPassword = "NuevaClave1", ConfirmPassword = "NuevaClave1"
        }, CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Ctl().ResetPassword(new ResetPasswordRequestDto
        {
            Email = "p@test.com", Token = encoded, NewPassword = "NuevaClave1", ConfirmPassword = "NuevaClave1"
        }, CancellationToken.None)));
    }
}

public class ActivitiesControllerTests : IDisposable
{
    private readonly AppTestHost _host = new();
    public void Dispose() => _host.Dispose();

    private ActivitiesController Ctl(Guid? user, params string[] roles) => AppTestHost.WithUser(
        new ActivitiesController(_host.Get<IActivityService>(), _host.Get<IActivityExecutionService>()), user, roles);

    private static CreateActivityDto Create(Guid moduleId) => new()
    {
        Titulo = "T", Descripcion = "D", ObjetivoPedagogico = "O", ModuloId = moduleId,
        TipoActividad = TipoActividad.SecuenciaLogica, ConfigJson = "{\"pasos\":[\"a\",\"b\"]}"
    };

    private static UpdateActivityDto Update(Guid moduleId) => new()
    {
        Titulo = "T2", Descripcion = "D", ObjetivoPedagogico = "O", ModuloId = moduleId,
        TipoActividad = TipoActividad.SecuenciaLogica, ConfigJson = "{\"pasos\":[\"a\",\"b\"]}"
    };

    [Fact]
    public async Task GetAll_DocenteVeInactivas_EstudianteNo()
    {
        var (tu, teacher) = await _host.CreateTeacherAsync();
        var module = await _host.CreateModuleAsync();
        await _host.CreateActivityAsync(module, teacher, "A");
        await _host.CreateActivityAsync(module, teacher, "B", active: false);

        var staff = (ApiResponse<List<ActivityDto>>)((OkObjectResult)await Ctl(tu.Id, "Docente").GetAll(onlyActive: false)).Value!;
        var student = (ApiResponse<List<ActivityDto>>)((OkObjectResult)await Ctl(Guid.NewGuid(), "Estudiante").GetAll(onlyActive: false)).Value!;

        Assert.Equal(2, staff.Data!.Count);
        Assert.Single(student.Data!);
    }

    [Fact]
    public async Task GetById_TodosLosCaminos()
    {
        var (tu, teacher) = await _host.CreateTeacherAsync();
        var activity = await _host.CreateActivityAsync(await _host.CreateModuleAsync(), teacher);

        Assert.Equal(200, Http.Code(await Ctl(tu.Id).GetById(activity.Id)));
        Assert.Equal(404, Http.Code(await Ctl(tu.Id).GetById(Guid.NewGuid())));
    }

    [Fact]
    public async Task Create_TodosLosCaminos()
    {
        var (tu, _) = await _host.CreateTeacherAsync();
        var module = await _host.CreateModuleAsync();

        Assert.Equal(401, Http.Code(await Ctl(null).Create(Create(module.Id))));
        Assert.Equal(400, Http.Code(await Http.Invalid(Ctl(tu.Id)).Create(Create(module.Id))));
        Assert.Equal(400, Http.Code(await Ctl(tu.Id).Create(Create(Guid.NewGuid()))));
        Assert.Equal(200, Http.Code(await Ctl(tu.Id).Create(Create(module.Id))));
    }

    [Fact]
    public async Task Update_TodosLosCaminos()
    {
        var (tu, teacher) = await _host.CreateTeacherAsync();
        var (otherUser, _) = await _host.CreateTeacherAsync();
        var module = await _host.CreateModuleAsync();
        var activity = await _host.CreateActivityAsync(module, teacher);

        Assert.Equal(401, Http.Code(await Ctl(null).Update(activity.Id, Update(module.Id))));
        Assert.Equal(400, Http.Code(await Http.Invalid(Ctl(tu.Id)).Update(activity.Id, Update(module.Id))));
        Assert.Equal(403, Http.Code(await Ctl(otherUser.Id, "Docente").Update(activity.Id, Update(module.Id))));
        Assert.Equal(400, Http.Code(await Ctl(tu.Id, "Docente").Update(Guid.NewGuid(), Update(module.Id))));
        Assert.Equal(200, Http.Code(await Ctl(tu.Id, "Docente").Update(activity.Id, Update(module.Id))));
        Assert.Equal(200, Http.Code(await Ctl(otherUser.Id, "Administrador").Update(activity.Id, Update(module.Id))));
    }

    [Fact]
    public async Task Delete_TodosLosCaminos()
    {
        var (tu, teacher) = await _host.CreateTeacherAsync();
        var (otherUser, _) = await _host.CreateTeacherAsync();
        var module = await _host.CreateModuleAsync();
        var a1 = await _host.CreateActivityAsync(module, teacher, "A1");
        var a2 = await _host.CreateActivityAsync(module, teacher, "A2");

        Assert.Equal(401, Http.Code(await Ctl(null).Delete(a1.Id)));
        Assert.Equal(403, Http.Code(await Ctl(otherUser.Id, "Docente").Delete(a1.Id)));
        Assert.Equal(404, Http.Code(await Ctl(tu.Id, "Docente").Delete(Guid.NewGuid())));
        Assert.Equal(200, Http.Code(await Ctl(tu.Id, "Docente").Delete(a1.Id)));
        Assert.Equal(200, Http.Code(await Ctl(otherUser.Id, "Administrador").Delete(a2.Id)));
    }

    [Fact]
    public async Task StartYSubmit_TodosLosCaminos()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var (su, student) = await _host.CreateStudentAsync();
        var activity = await _host.CreateActivityAsync(await _host.CreateModuleAsync(), teacher);
        var assignment = await _host.CreateAssignmentAsync(activity, student);
        var dto = new SubmitActivityDto { CantidadAciertos = 2 };

        Assert.Equal(401, Http.Code(await Ctl(null).StartActivity(assignment.Id)));
        Assert.Equal(403, Http.Code(await Ctl(Guid.NewGuid(), "Estudiante").StartActivity(assignment.Id)));
        Assert.Equal(400, Http.Code(await Ctl(su.Id, "Estudiante").StartActivity(Guid.NewGuid())));
        Assert.Equal(200, Http.Code(await Ctl(su.Id, "Estudiante").StartActivity(assignment.Id)));

        Assert.Equal(401, Http.Code(await Ctl(null).SubmitActivity(assignment.Id, dto)));
        Assert.Equal(400, Http.Code(await Http.Invalid(Ctl(su.Id)).SubmitActivity(assignment.Id, dto)));
        Assert.Equal(403, Http.Code(await Ctl(Guid.NewGuid(), "Estudiante").SubmitActivity(assignment.Id, dto)));
        Assert.Equal(400, Http.Code(await Ctl(su.Id, "Estudiante").SubmitActivity(assignment.Id, new SubmitActivityDto { CompletadaExitosamente = true, CantidadAciertos = 0 })));
        Assert.Equal(200, Http.Code(await Ctl(su.Id, "Estudiante").SubmitActivity(assignment.Id, dto)));
    }
}

public class AssignmentsControllerTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private readonly FakeReportService _reports = new();
    public void Dispose() => _host.Dispose();

    private AssignmentsController Ctl(Guid? user, params string[] roles) => AppTestHost.WithUser(
        new AssignmentsController(_host.Get<IAssignmentService>(), _reports), user, roles);

    private async Task<(ApplicationUser TeacherUser, ApplicationUser GuardianUser, ApplicationUser StudentUser, StudentProfile Student, Activity Activity, ActivityAssignment Assignment)> SeedAsync()
    {
        var (tu, teacher) = await _host.CreateTeacherAsync();
        var (gu, guardian) = await _host.CreateGuardianAsync();
        var classroom = await _host.CreateClassroomAsync(teacher);
        var (su, student) = await _host.CreateStudentAsync(classroom, guardian);
        var activity = await _host.CreateActivityAsync(await _host.CreateModuleAsync(), teacher);
        var assignment = await _host.CreateAssignmentAsync(activity, student);
        return (tu, gu, su, student, activity, assignment);
    }

    [Fact]
    public async Task Adaptive_TodosLosCaminos()
    {
        var (tu, _, _, _, _, _) = await SeedAsync();

        Assert.Equal(401, Http.Code(await Ctl(null).CreateAdaptiveRoutine(null, CancellationToken.None)));
        _reports.Succeed = false;
        Assert.Equal(400, Http.Code(await Ctl(tu.Id).CreateAdaptiveRoutine(new AdaptiveRoutineRequestDto(), CancellationToken.None)));
        _reports.Succeed = true;
        Assert.Equal(200, Http.Code(await Ctl(tu.Id).CreateAdaptiveRoutine(null, CancellationToken.None)));
    }

    [Fact]
    public async Task Create_TodosLosCaminos()
    {
        var (tu, _, _, student, activity, _) = await SeedAsync();

        Assert.Equal(400, Http.Code(await Http.Invalid(Ctl(tu.Id)).Create(new CreateAssignmentDto())));
        Assert.Equal(400, Http.Code(await Ctl(tu.Id).Create(new CreateAssignmentDto { ActivityId = Guid.NewGuid() })));
        Assert.Equal(200, Http.Code(await Ctl(tu.Id).Create(new CreateAssignmentDto { ActivityId = activity.Id, StudentProfileId = student.Id })));
    }

    [Fact]
    public async Task GetAll_PersonalYAcudiente()
    {
        var (tu, gu, _, student, _, _) = await SeedAsync();
        var (_, other) = await _host.CreateStudentAsync();

        Assert.Equal(200, Http.Code(await Ctl(tu.Id, "Docente").GetAll()));
        Assert.Equal(200, Http.Code(await Ctl(gu.Id, "Acudiente").GetAll(student.Id)));
        Assert.Equal(403, Http.Code(await Ctl(gu.Id, "Acudiente").GetAll(other.Id)));
        Assert.Equal(404, Http.Code(await Ctl(gu.Id, "Acudiente").GetAll(Guid.NewGuid())));
    }

    [Fact]
    public async Task GetById_TodosLosCaminos()
    {
        var (tu, _, su, _, _, assignment) = await SeedAsync();

        Assert.Equal(200, Http.Code(await Ctl(tu.Id, "Docente").GetById(assignment.Id)));
        Assert.Equal(200, Http.Code(await Ctl(su.Id, "Estudiante").GetById(assignment.Id)));
        Assert.Equal(403, Http.Code(await Ctl(Guid.NewGuid(), "Estudiante").GetById(assignment.Id)));
        Assert.Equal(404, Http.Code(await Ctl(tu.Id, "Docente").GetById(Guid.NewGuid())));
    }

    [Fact]
    public async Task Delete_TodosLosCaminos()
    {
        var (tu, _, _, student, activity, assignment) = await SeedAsync();
        var (otherUser, _) = await _host.CreateTeacherAsync();
        var second = await _host.CreateAssignmentAsync(await _host.CreateActivityAsync(await _host.CreateModuleAsync(name: "M2"), (await _host.Context.TeacherProfiles.FirstAsync(t => t.UserId == tu.Id)), "Otra"), student, orden: 2);

        Assert.Equal(403, Http.Code(await Ctl(otherUser.Id, "Docente").Delete(assignment.Id)));
        Assert.Equal(404, Http.Code(await Ctl(tu.Id, "Docente").Delete(Guid.NewGuid())));
        Assert.Equal(200, Http.Code(await Ctl(tu.Id, "Docente").Delete(assignment.Id)));
        Assert.Equal(200, Http.Code(await Ctl(otherUser.Id, "Administrador").Delete(second.Id)));
        Assert.NotNull(activity);
    }

    [Fact]
    public async Task Grade_TodosLosCaminos()
    {
        var (tu, _, _, _, _, assignment) = await SeedAsync();
        var (otherUser, _) = await _host.CreateTeacherAsync();
        var dto = new GradeAssignmentDto { Calificacion = 4 };

        Assert.Equal(400, Http.Code(await Http.Invalid(Ctl(tu.Id)).Grade(assignment.Id, dto, CancellationToken.None)));
        Assert.Equal(401, Http.Code(await Ctl(null).Grade(assignment.Id, dto, CancellationToken.None)));
        Assert.Equal(403, Http.Code(await Ctl(otherUser.Id, "Docente").Grade(assignment.Id, dto, CancellationToken.None)));
        Assert.Equal(404, Http.Code(await Ctl(tu.Id, "Docente").Grade(Guid.NewGuid(), dto, CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Ctl(tu.Id, "Docente").Grade(assignment.Id, dto, CancellationToken.None)));
    }

    [Fact]
    public async Task RemoveGrade_TodosLosCaminos()
    {
        var (tu, _, _, _, _, assignment) = await SeedAsync();
        var (otherUser, _) = await _host.CreateTeacherAsync();

        Assert.Equal(401, Http.Code(await Ctl(null).RemoveGrade(assignment.Id, CancellationToken.None)));
        Assert.Equal(403, Http.Code(await Ctl(otherUser.Id, "Docente").RemoveGrade(assignment.Id, CancellationToken.None)));
        Assert.Equal(404, Http.Code(await Ctl(tu.Id, "Docente").RemoveGrade(Guid.NewGuid(), CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Ctl(tu.Id, "Docente").RemoveGrade(assignment.Id, CancellationToken.None)));
    }
}

public class ModulesAndCategoriesControllerTests : IDisposable
{
    private readonly AppTestHost _host = new();
    public void Dispose() => _host.Dispose();

    private ModulesController Modules() => AppTestHost.WithUser(new ModulesController(_host.Get<IModuleService>()), Guid.NewGuid(), "Administrador");
    private CategoriesController Categories() => AppTestHost.WithUser(new CategoriesController(_host.Get<ICategoryService>()), Guid.NewGuid(), "Administrador");

    [Fact]
    public async Task Modules_TodosLosCaminos()
    {
        var created = await Modules().Create(new CreateModuleDto { Titulo = "M", Descripcion = "d" });
        var id = ((ApiResponse<ModuleDto>)((OkObjectResult)created).Value!).Data!.Id;

        Assert.Equal(400, Http.Code(await Http.Invalid(Modules()).Create(new CreateModuleDto())));
        Assert.Equal(200, Http.Code(created));
        Assert.Equal(200, Http.Code(await Modules().GetAll()));
        Assert.Equal(200, Http.Code(await Modules().GetById(id)));
        Assert.Equal(404, Http.Code(await Modules().GetById(Guid.NewGuid())));
        Assert.Equal(400, Http.Code(await Http.Invalid(Modules()).Update(id, new UpdateModuleDto())));
        Assert.Equal(400, Http.Code(await Modules().Update(Guid.NewGuid(), new UpdateModuleDto { Titulo = "x", Descripcion = "y" })));
        Assert.Equal(200, Http.Code(await Modules().Update(id, new UpdateModuleDto { Titulo = "M2", Descripcion = "d" })));
        Assert.Equal(404, Http.Code(await Modules().Delete(Guid.NewGuid())));
        Assert.Equal(200, Http.Code(await Modules().Delete(id)));
    }

    [Fact]
    public async Task Categories_TodosLosCaminos()
    {
        var created = await Categories().Create(new CreateCategoryDto { Nombre = "C" });
        var id = ((ApiResponse<CategoryDto>)((OkObjectResult)created).Value!).Data!.Id;

        Assert.Equal(400, Http.Code(await Http.Invalid(Categories()).Create(new CreateCategoryDto())));
        Assert.Equal(400, Http.Code(await Categories().Create(new CreateCategoryDto { Nombre = "c" }))); // duplicada
        Assert.Equal(200, Http.Code(await Categories().GetAll()));
        Assert.Equal(200, Http.Code(await Categories().GetById(id)));
        Assert.Equal(404, Http.Code(await Categories().GetById(Guid.NewGuid())));
        Assert.Equal(400, Http.Code(await Http.Invalid(Categories()).Update(id, new UpdateCategoryDto())));
        Assert.Equal(400, Http.Code(await Categories().Update(Guid.NewGuid(), new UpdateCategoryDto { Nombre = "x" })));
        Assert.Equal(200, Http.Code(await Categories().Update(id, new UpdateCategoryDto { Nombre = "C2" })));
        Assert.Equal(400, Http.Code(await Categories().Delete(Guid.NewGuid())));
        Assert.Equal(200, Http.Code(await Categories().Delete(id)));
    }
}

public class StudentsAndSensoryAndNotesControllerTests : IDisposable
{
    private readonly AppTestHost _host = new();
    public void Dispose() => _host.Dispose();

    private StudentsController Students(Guid? user, params string[] roles) => AppTestHost.WithUser(new StudentsController(
        _host.Get<IStudentService>(), _host.Get<IAchievementService>(), _host.Get<IAssignmentService>(),
        _host.Get<IActivityAssignmentRepository>()), user, roles);

    private SensoryPreferencesController Sensory(Guid? user, params string[] roles)
        => AppTestHost.WithUser(new SensoryPreferencesController(_host.Get<ISensoryPreferenceService>()), user, roles);

    private PedagogicalNotesController Notes(Guid? user, params string[] roles)
        => AppTestHost.WithUser(new PedagogicalNotesController(_host.Get<IPedagogicalNoteService>()), user, roles);

    [Fact]
    public async Task Students_Me_TodosLosCaminos()
    {
        var (su, _) = await _host.CreateStudentAsync();

        Assert.Equal(401, Http.Code(await Students(null).GetMyDailyRoutine()));
        Assert.Equal(404, Http.Code(await Students(Guid.NewGuid()).GetMyDailyRoutine()));
        Assert.Equal(200, Http.Code(await Students(su.Id).GetMyDailyRoutine(DateTime.UtcNow)));

        Assert.Equal(401, Http.Code(await Students(null).GetMyProfile()));
        Assert.Equal(404, Http.Code(await Students(Guid.NewGuid()).GetMyProfile()));
        Assert.Equal(200, Http.Code(await Students(su.Id).GetMyProfile()));

        Assert.Equal(401, Http.Code(await Students(null).GetMyAchievements(CancellationToken.None)));
        Assert.Equal(404, Http.Code(await Students(Guid.NewGuid()).GetMyAchievements(CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Students(su.Id).GetMyAchievements(CancellationToken.None)));
    }

    [Fact]
    public async Task Students_GetAll_YGrades()
    {
        var (gu, guardian) = await _host.CreateGuardianAsync();
        var (tu, _) = await _host.CreateTeacherAsync();
        var (_, student) = await _host.CreateStudentAsync(guardian: guardian);

        Assert.Equal(200, Http.Code(await Students(gu.Id, "Acudiente").GetAll()));
        Assert.Equal(200, Http.Code(await Students(tu.Id, "Docente").GetAll()));
        Assert.Equal(401, Http.Code(await Students(null).GetStudentGrades(student.Id, CancellationToken.None)));
        Assert.Equal(404, Http.Code(await Students(tu.Id).GetStudentGrades(Guid.NewGuid(), CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Students(tu.Id).GetStudentGrades(student.Id, CancellationToken.None)));
    }

    [Fact]
    public async Task Sensory_TodosLosCaminos()
    {
        var (gu, guardian) = await _host.CreateGuardianAsync();
        var (_, student) = await _host.CreateStudentAsync(guardian: guardian);
        var (_, stranger) = await _host.CreateStudentAsync();
        var dto = new UpdateSensoryPreferenceDto { PaletaColor = "Neutro", Tipografia = "Arial", NivelEscalaFuente = 3 };

        Assert.Equal(401, Http.Code(await Sensory(null).GetPreferences(student.Id)));
        Assert.Equal(200, Http.Code(await Sensory(gu.Id, "Acudiente").GetPreferences(student.Id)));
        Assert.Equal(403, Http.Code(await Sensory(gu.Id, "Acudiente").GetPreferences(stranger.Id)));
        Assert.Equal(404, Http.Code(await Sensory(gu.Id, "Acudiente").GetPreferences(Guid.NewGuid())));

        Assert.Equal(401, Http.Code(await Sensory(null).UpdatePreferences(student.Id, dto)));
        Assert.Equal(400, Http.Code(await Http.Invalid(Sensory(gu.Id)).UpdatePreferences(student.Id, dto)));
        Assert.Equal(403, Http.Code(await Sensory(gu.Id, "Acudiente").UpdatePreferences(stranger.Id, dto)));
        Assert.Equal(404, Http.Code(await Sensory(gu.Id, "Acudiente").UpdatePreferences(Guid.NewGuid(), dto)));
        Assert.Equal(200, Http.Code(await Sensory(gu.Id, "Acudiente").UpdatePreferences(student.Id, dto)));
    }

    [Fact]
    public async Task Notes_TodosLosCaminos()
    {
        var (tu, _) = await _host.CreateTeacherAsync();
        var (gu, guardian) = await _host.CreateGuardianAsync();
        var (_, student) = await _host.CreateStudentAsync(guardian: guardian);
        var dto = new CreatePedagogicalNoteDto { Contenido = "Observación" };

        Assert.Equal(400, Http.Code(await Http.Invalid(Notes(tu.Id)).CreateNote(student.Id, dto, CancellationToken.None)));
        Assert.Equal(401, Http.Code(await Notes(null).CreateNote(student.Id, dto, CancellationToken.None)));
        Assert.Equal(404, Http.Code(await Notes(tu.Id).CreateNote(Guid.NewGuid(), dto, CancellationToken.None)));
        Assert.Equal(400, Http.Code(await Notes(tu.Id).CreateNote(student.Id, new CreatePedagogicalNoteDto { Contenido = "x", Fecha = "mala" }, CancellationToken.None)));
        var created = await Notes(tu.Id, "Docente").CreateNote(student.Id, dto, CancellationToken.None);
        Assert.Equal(201, Http.Code(created));
        var noteId = ((ApiResponse<PedagogicalNoteDto>)((ObjectResult)created).Value!).Data!.Id;

        Assert.Equal(401, Http.Code(await Notes(null).GetNotes(student.Id, CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Notes(gu.Id, "Acudiente").GetNotes(student.Id, CancellationToken.None)));
        Assert.Equal(403, Http.Code(await Notes(Guid.NewGuid(), "Acudiente").GetNotes(student.Id, CancellationToken.None)));
        Assert.Equal(404, Http.Code(await Notes(tu.Id, "Docente").GetNotes(Guid.NewGuid(), CancellationToken.None)));

        Assert.Equal(401, Http.Code(await Notes(null).UpdateNote(noteId, dto, CancellationToken.None)));
        Assert.Equal(404, Http.Code(await Notes(tu.Id, "Docente").UpdateNote(Guid.NewGuid(), dto, CancellationToken.None)));
        var (otherTeacher, _) = await _host.CreateTeacherAsync();
        Assert.Equal(403, Http.Code(await Notes(otherTeacher.Id, "Docente").UpdateNote(noteId, dto, CancellationToken.None)));
        Assert.Equal(400, Http.Code(await Notes(tu.Id, "Docente").UpdateNote(noteId, new CreatePedagogicalNoteDto { Contenido = "x", Fecha = "mala" }, CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Notes(tu.Id, "Docente").UpdateNote(noteId, dto, CancellationToken.None)));

        Assert.Equal(401, Http.Code(await Notes(null).DeleteNote(noteId, CancellationToken.None)));
        Assert.Equal(403, Http.Code(await Notes(otherTeacher.Id, "Docente").DeleteNote(noteId, CancellationToken.None)));
        Assert.Equal(404, Http.Code(await Notes(tu.Id, "Docente").DeleteNote(Guid.NewGuid(), CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Notes(tu.Id, "Docente").DeleteNote(noteId, CancellationToken.None)));
    }
}

public class GuardianAndAnalyticsAndReportsControllerTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private readonly FakeReportService _reports = new();
    public void Dispose() => _host.Dispose();

    private GuardianController Guardian(Guid? user, params string[] roles) => AppTestHost.WithUser(new GuardianController(
        _host.Get<IGuardianMonitoringService>(), _host.Get<IPedagogicalNoteService>(), _host.Get<IUserService>()), user, roles);

    private AnalyticsController Analytics() => AppTestHost.WithUser(
        new AnalyticsController(_host.Get<ITeacherAnalyticsService>()), Guid.NewGuid(), "Docente");

    private ReportsController Reports(Guid? user, params string[] roles)
        => AppTestHost.WithUser(new ReportsController(_reports), user, roles);

    [Fact]
    public async Task Guardian_ProgressHistoryYObservations()
    {
        var (gu, guardian) = await _host.CreateGuardianAsync();
        var (_, student) = await _host.CreateStudentAsync(guardian: guardian);
        var (_, stranger) = await _host.CreateStudentAsync();

        Assert.Equal(401, Http.Code(await Guardian(null).GetStudentProgress(student.Id, CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Guardian(gu.Id, "Acudiente").GetStudentProgress(student.Id, CancellationToken.None)));
        Assert.Equal(403, Http.Code(await Guardian(gu.Id, "Acudiente").GetStudentProgress(stranger.Id, CancellationToken.None)));
        Assert.Equal(404, Http.Code(await Guardian(gu.Id, "Acudiente").GetStudentProgress(Guid.NewGuid(), CancellationToken.None)));

        Assert.Equal(401, Http.Code(await Guardian(null).GetStudentActivityHistory(student.Id)));
        Assert.Equal(200, Http.Code(await Guardian(gu.Id, "Acudiente").GetStudentActivityHistory(student.Id)));
        Assert.Equal(403, Http.Code(await Guardian(gu.Id, "Acudiente").GetStudentActivityHistory(stranger.Id)));
        Assert.Equal(404, Http.Code(await Guardian(gu.Id, "Acudiente").GetStudentActivityHistory(Guid.NewGuid())));

        Assert.Equal(401, Http.Code(await Guardian(null).GetObservations(student.Id)));
        Assert.Equal(200, Http.Code(await Guardian(gu.Id, "Acudiente").GetObservations(student.Id)));
        Assert.Equal(403, Http.Code(await Guardian(gu.Id, "Acudiente").GetObservations(stranger.Id)));
        Assert.Equal(404, Http.Code(await Guardian(gu.Id, "Acudiente").GetObservations(Guid.NewGuid())));
    }

    [Fact]
    public async Task Guardian_ResetPassword_TodosLosCaminos()
    {
        var (gu, guardian) = await _host.CreateGuardianAsync();
        var (_, student) = await _host.CreateStudentAsync(guardian: guardian);
        var (_, stranger) = await _host.CreateStudentAsync();
        var ok = new ResetStudentPasswordDto { NewPassword = "NuevaClave1", ConfirmPassword = "NuevaClave1" };

        Assert.Equal(400, Http.Code(await Http.Invalid(Guardian(gu.Id)).ResetStudentPassword(student.Id, ok)));
        Assert.Equal(401, Http.Code(await Guardian(null).ResetStudentPassword(student.Id, ok)));
        Assert.Equal(403, Http.Code(await Guardian(gu.Id, "Acudiente").ResetStudentPassword(stranger.Id, ok)));
        Assert.Equal(404, Http.Code(await Guardian(gu.Id, "Acudiente").ResetStudentPassword(Guid.NewGuid(), ok)));
        Assert.Equal(400, Http.Code(await Guardian(gu.Id, "Acudiente").ResetStudentPassword(student.Id, new ResetStudentPasswordDto { NewPassword = "abc", ConfirmPassword = "abc" })));
        Assert.Equal(200, Http.Code(await Guardian(gu.Id, "Acudiente").ResetStudentPassword(student.Id, ok)));
    }

    [Fact]
    public async Task Analytics_TodosLosCaminos()
    {
        var classroom = await _host.CreateClassroomAsync();
        var (_, student) = await _host.CreateStudentAsync(classroom);

        Assert.Equal(200, Http.Code(await Analytics().GetStudentSummary(student.Id, CancellationToken.None)));
        Assert.Equal(404, Http.Code(await Analytics().GetStudentSummary(Guid.NewGuid(), CancellationToken.None)));
        Assert.Equal(200, Http.Code(await Analytics().GetClassroomSummary(classroom.Id, CancellationToken.None)));
        Assert.Equal(404, Http.Code(await Analytics().GetClassroomSummary(Guid.NewGuid(), CancellationToken.None)));
    }

    [Fact]
    public async Task Reports_TodosLosCaminos()
    {
        var id = Guid.NewGuid();

        // Estudiante: sin usuario / éxito / no encontrado / sin autorización
        Assert.Equal(401, Http.Code(await Reports(null).GetStudentReport(id)));
        Assert.Equal(200, Http.Code(await Reports(id, "Docente").GetStudentReport(id)));
        Assert.Equal(200, Http.Code(await Reports(id, "Acudiente").GetCurrentOrSelectedStudentReport()));
        _reports.Succeed = false;
        _reports.FailMessage = "No se encontró";
        Assert.Equal(404, Http.Code(await Reports(id, "Docente").GetStudentReport(id)));
        _reports.FailMessage = "Sin autorización para este estudiante";
        Assert.Equal(403, Http.Code(await Reports(id, "Acudiente").GetStudentReport(id)));
        Assert.Equal(401, Http.Code(await Reports(null).GetCurrentOrSelectedStudentReport(id)));

        // Aula
        Assert.Equal(401, Http.Code(await Reports(null).GetClassroomReportById(id)));
        Assert.Equal(401, Http.Code(await Reports(null).GetMyClassroomReport()));
        Assert.Equal(404, Http.Code(await Reports(id, "Docente").GetClassroomReportById(id)));
        Assert.Equal(404, Http.Code(await Reports(id, "Docente").GetMyClassroomReport()));
        _reports.Succeed = true;
        Assert.Equal(200, Http.Code(await Reports(id, "Docente").GetClassroomReportById(id)));
        Assert.Equal(200, Http.Code(await Reports(id, "Docente").GetMyClassroomReport(id)));

        // Alertas y recálculo
        Assert.Equal(401, Http.Code(await Reports(null).EvaluateAlerts(null)));
        Assert.Equal(200, Http.Code(await Reports(id, "Docente").EvaluateAlerts(null)));
        Assert.Equal(200, Http.Code(await Reports(id, "Docente").EvaluateAlerts(new EvaluateTeaAlertsRequestDto())));
        Assert.Equal(200, Http.Code(await Reports(id, "Docente").RecalculateStudentProgress(id)));
        _reports.Succeed = false;
        Assert.Equal(404, Http.Code(await Reports(id, "Docente").RecalculateStudentProgress(id)));
    }
}

/// <summary>IReportService falso (los reales dependen de procedimientos almacenados de SQL Server).</summary>
internal sealed class FakeReportService : IReportService
{
    public bool Succeed { get; set; } = true;
    public string FailMessage { get; set; } = "Fallo simulado";

    private ApiResponse<T> Result<T>(T data) where T : class
        => Succeed ? ApiResponse<T>.Ok(data) : ApiResponse<T>.Fail(FailMessage);

    public Task<ApiResponse<StudentComprehensiveReportDto>> GetStudentComprehensiveReportAsync(
        Guid? studentProfileId, Guid requesterUserId, bool isGuardian, int diasVentana = 30, CancellationToken cancellationToken = default)
        => Task.FromResult(Result(new StudentComprehensiveReportDto()));

    public Task<ApiResponse<ClassroomConsolidatedReportDto>> GetClassroomConsolidatedReportAsync(
        Guid? classroomId, Guid requesterUserId, int diasVentana = 30, CancellationToken cancellationToken = default)
        => Task.FromResult(Result(new ClassroomConsolidatedReportDto()));

    public Task<ApiResponse<List<AdaptiveRoutineAssignmentResultDto>>> AssignAdaptiveRoutineByClassroomAsync(
        Guid requesterUserId, AdaptiveRoutineRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(Result(new List<AdaptiveRoutineAssignmentResultDto>()));

    public Task<ApiResponse<List<TeaAlertEvaluationItemDto>>> EvaluateTeaAlertsAndRecommendationsAsync(
        Guid requesterUserId, EvaluateTeaAlertsRequestDto request, CancellationToken cancellationToken = default)
        => Task.FromResult(Result(new List<TeaAlertEvaluationItemDto>()));

    public Task<ApiResponse<RecalculateStudentProgressDto>> RecalculateStudentProgressAndAchievementsAsync(
        Guid studentProfileId, CancellationToken cancellationToken = default)
        => Task.FromResult(Result(new RecalculateStudentProgressDto()));
}
