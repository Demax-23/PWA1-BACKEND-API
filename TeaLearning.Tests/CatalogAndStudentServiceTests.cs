using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Core.Enums;
using TeaLearning.Services.DTOs.Categories;
using TeaLearning.Services.DTOs.Modules;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Tests;

public class CategoryServiceTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private ICategoryService Svc => _host.Get<ICategoryService>();

    public void Dispose() => _host.Dispose();

    private static CreateCategoryDto NewDto(string name, int orden = 1) => new() { Nombre = name, Orden = orden };

    [Fact]
    public async Task Create_ValoresPorDefectoYRecorte()
    {
        var result = await Svc.CreateCategoryAsync(new CreateCategoryDto { Nombre = "  Rutinas ", Descripcion = null, Color = "  ", Orden = 1 });

        Assert.True(result.Success);
        Assert.Equal("Rutinas", result.Data!.Nombre);
        Assert.Equal(string.Empty, result.Data.Descripcion);
        Assert.Equal("#2F6E5C", result.Data.Color);
    }

    [Fact]
    public async Task Create_ConColorYDescripcion_LosConserva()
    {
        var result = await Svc.CreateCategoryAsync(new CreateCategoryDto { Nombre = "Arte", Descripcion = " d ", Color = " #FFFFFF ", Orden = 1, IsActive = false });

        Assert.Equal("#FFFFFF", result.Data!.Color);
        Assert.Equal("d", result.Data.Descripcion);
        Assert.False(result.Data.IsActive);
    }

    [Fact]
    public async Task Create_NombreDuplicadoSinImportarMayusculas_Falla()
    {
        await Svc.CreateCategoryAsync(NewDto("Rutinas"));

        var result = await Svc.CreateCategoryAsync(NewDto("  RUTINAS "));

        Assert.False(result.Success);
        Assert.Contains("Ya existe", result.Message);
    }

    [Fact]
    public async Task Create_ConOrdenOcupado_DesplazaLasExistentes()
    {
        await Svc.CreateCategoryAsync(NewDto("A", 1));
        await Svc.CreateCategoryAsync(NewDto("B", 2));

        await Svc.CreateCategoryAsync(NewDto("Nueva", 1));

        var all = (await Svc.GetAllCategoriesAsync()).Data!;
        Assert.Equal(new[] { "Nueva", "A", "B" }, all.Select(c => c.Nombre));
        Assert.Equal(new[] { 1, 2, 3 }, all.Select(c => c.Orden));
    }

    [Fact]
    public async Task GetAll_SoloActivasONoSegunParametro_YContadorDeModulosActivos()
    {
        var active = await _host.CreateCategoryAsync("Activa");
        await _host.CreateCategoryAsync("Inactiva", active: false);
        await _host.CreateModuleAsync(active, "M1");
        await _host.CreateModuleAsync(active, "M2", active: false);

        var onlyActive = (await Svc.GetAllCategoriesAsync(onlyActive: true)).Data!;
        var all = (await Svc.GetAllCategoriesAsync(onlyActive: false)).Data!;

        Assert.Single(onlyActive);
        Assert.Equal(1, onlyActive[0].TotalModulos);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetById_ExistenteEInexistente()
    {
        var category = await _host.CreateCategoryAsync();

        Assert.True((await Svc.GetCategoryByIdAsync(category.Id)).Success);
        Assert.False((await Svc.GetCategoryByIdAsync(Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Update_Inexistente_Falla()
    {
        Assert.False((await Svc.UpdateCategoryAsync(Guid.NewGuid(), new UpdateCategoryDto { Nombre = "x" })).Success);
    }

    [Fact]
    public async Task Update_NombreDeOtraCategoria_Falla()
    {
        await _host.CreateCategoryAsync("Existente");
        var other = await _host.CreateCategoryAsync("Otra");

        var result = await Svc.UpdateCategoryAsync(other.Id, new UpdateCategoryDto { Nombre = "existente" });

        Assert.False(result.Success);
        Assert.Contains("otra categoría", result.Message);
    }

    [Fact]
    public async Task Update_MismoNombre_Permitido_YCambiaCampos()
    {
        var category = await _host.CreateCategoryAsync("Original");

        var result = await Svc.UpdateCategoryAsync(category.Id, new UpdateCategoryDto
        {
            Nombre = "Original",
            Descripcion = null,
            Color = null,
            Orden = category.Orden,
            IsActive = false
        });

        Assert.True(result.Success);
        Assert.Equal("#2F6E5C", result.Data!.Color);
        Assert.False(result.Data.IsActive);
    }

    [Theory]
    [InlineData(3, 1)] // sube: los intermedios bajan una posición
    [InlineData(1, 3)] // baja
    public async Task Update_CambioDeOrden_ReordenaLasDemas(int from, int to)
    {
        for (var i = 1; i <= 3; i++)
        {
            _host.Context.LearningCategories.Add(new LearningCategory { Nombre = "C" + i, Orden = i });
        }
        await _host.Context.SaveChangesAsync();
        var moving = await _host.Context.LearningCategories.SingleAsync(c => c.Orden == from);

        var result = await Svc.UpdateCategoryAsync(moving.Id, new UpdateCategoryDto { Nombre = moving.Nombre, Orden = to });

        Assert.True(result.Success);
        var orders = _host.Context.LearningCategories.AsNoTracking().Select(c => c.Orden).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, orders.OrderBy(o => o));
        Assert.Equal(to, (await _host.Context.LearningCategories.AsNoTracking().SingleAsync(c => c.Id == moving.Id)).Orden);
    }

    [Fact]
    public async Task Delete_Inexistente_Falla()
    {
        Assert.False((await Svc.DeleteCategoryAsync(Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Delete_ConModulosActivos_Falla()
    {
        var category = await _host.CreateCategoryAsync();
        await _host.CreateModuleAsync(category);

        var result = await Svc.DeleteCategoryAsync(category.Id);

        Assert.False(result.Success);
        Assert.Contains("módulos temáticos asociados", result.Message);
    }

    [Fact]
    public async Task Delete_SoloConModulosInactivos_LosDesvinculaYElimina()
    {
        var category = await _host.CreateCategoryAsync();
        var module = await _host.CreateModuleAsync(category, active: false);

        var result = await Svc.DeleteCategoryAsync(category.Id);

        Assert.True(result.Success);
        Assert.Equal(0, await _host.Context.LearningCategories.CountAsync());
        Assert.Null((await _host.Context.LearningModules.AsNoTracking().SingleAsync(m => m.Id == module.Id)).CategoriaId);
    }

    [Fact]
    public async Task Delete_SinModulos_Elimina()
    {
        var category = await _host.CreateCategoryAsync();

        Assert.True((await Svc.DeleteCategoryAsync(category.Id)).Success);
    }
}

public class ModuleServiceTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private IModuleService Svc => _host.Get<IModuleService>();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task GetAll_FiltraActivosYContabilizaActividadesActivas()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var category = await _host.CreateCategoryAsync();
        var m1 = await _host.CreateModuleAsync(category, "Activo");
        await _host.CreateModuleAsync(null, "Inactivo", active: false);
        await _host.CreateActivityAsync(m1, teacher, "A1");
        await _host.CreateActivityAsync(m1, teacher, "A2", active: false);

        _host.Context.ChangeTracker.Clear(); // evita que el fixup de entidades rastreadas contamine el Include filtrado

        var onlyActive = (await Svc.GetAllModulesAsync()).Data!;
        var all = (await Svc.GetAllModulesAsync(onlyActive: false)).Data!;

        Assert.Single(onlyActive);
        Assert.Equal(1, onlyActive[0].TotalActividades);
        Assert.Equal(category.Nombre, onlyActive[0].CategoriaNombre);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task GetById_DevuelveDetalleConActividades_OFalla()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var module = await _host.CreateModuleAsync();
        await _host.CreateActivityAsync(module, teacher);
        var inactive = await _host.CreateModuleAsync(name: "Inactivo", active: false);

        var ok = await Svc.GetModuleByIdAsync(module.Id);

        Assert.True(ok.Success);
        Assert.Single(ok.Data!.Activities);
        Assert.False((await Svc.GetModuleByIdAsync(Guid.NewGuid())).Success);
        Assert.False((await Svc.GetModuleByIdAsync(inactive.Id)).Success);
    }

    [Fact]
    public async Task Create_SinCategoria_Persiste()
    {
        var result = await Svc.CreateModuleAsync(new CreateModuleDto { Titulo = " Módulo ", Descripcion = " Desc ", Icono = " star ", Orden = 1 });

        Assert.True(result.Success);
        Assert.Equal("Módulo", result.Data!.Titulo);
        Assert.Equal("star", result.Data.Icono);
        Assert.Null(result.Data.CategoriaId);
    }

    [Fact]
    public async Task Create_ConCategoria_DesplazaOrdenesExistentes()
    {
        var category = await _host.CreateCategoryAsync();
        await Svc.CreateModuleAsync(new CreateModuleDto { Titulo = "A", Descripcion = "d", Orden = 1, CategoriaId = category.Id });

        await Svc.CreateModuleAsync(new CreateModuleDto { Titulo = "B", Descripcion = "d", Orden = 1, CategoriaId = category.Id });

        var orders = _host.Context.LearningModules.AsNoTracking().OrderBy(m => m.Orden).Select(m => m.Titulo).ToList();
        Assert.Equal(new[] { "B", "A" }, orders);
    }

    [Fact]
    public async Task Update_Inexistente_Falla()
    {
        Assert.False((await Svc.UpdateModuleAsync(Guid.NewGuid(), new UpdateModuleDto { Titulo = "x", Descripcion = "y" })).Success);
    }

    [Fact]
    public async Task Update_CambiaDatosSinCategoria()
    {
        var module = await _host.CreateModuleAsync();

        var result = await Svc.UpdateModuleAsync(module.Id, new UpdateModuleDto { Titulo = " Nuevo ", Descripcion = " D ", Icono = " i ", Orden = 4, IsActive = false });

        Assert.True(result.Success);
        Assert.Equal("Nuevo", result.Data!.Titulo);
        Assert.False(result.Data.IsActive);
        Assert.Equal(4, result.Data.Orden);
    }

    [Fact]
    public async Task Update_MismaCategoriaConOtroOrden_Reordena()
    {
        var category = await _host.CreateCategoryAsync();
        for (var i = 1; i <= 3; i++)
        {
            _host.Context.LearningModules.Add(new LearningModule { Titulo = "M" + i, Orden = i, CategoriaId = category.Id });
        }
        await _host.Context.SaveChangesAsync();
        var third = await _host.Context.LearningModules.SingleAsync(m => m.Orden == 3);
        var first = await _host.Context.LearningModules.SingleAsync(m => m.Orden == 1);

        await Svc.UpdateModuleAsync(third.Id, new UpdateModuleDto { Titulo = "M3", Descripcion = "d", Orden = 1, CategoriaId = category.Id });
        await Svc.UpdateModuleAsync(first.Id, new UpdateModuleDto { Titulo = "M1", Descripcion = "d", Orden = 3, CategoriaId = category.Id });

        var orders = _host.Context.LearningModules.AsNoTracking().Select(m => m.Orden).ToList();
        Assert.Equal(new[] { 1, 2, 3 }, orders.OrderBy(o => o));
    }

    [Fact]
    public async Task Update_CambioDeCategoria_DesplazaEnLaNueva()
    {
        var c1 = await _host.CreateCategoryAsync("C1");
        var c2 = await _host.CreateCategoryAsync("C2");
        var existing = new LearningModule { Titulo = "Existente", Orden = 1, CategoriaId = c2.Id };
        var moving = new LearningModule { Titulo = "Mueve", Orden = 1, CategoriaId = c1.Id };
        _host.Context.LearningModules.AddRange(existing, moving);
        await _host.Context.SaveChangesAsync();

        var result = await Svc.UpdateModuleAsync(moving.Id, new UpdateModuleDto { Titulo = "Mueve", Descripcion = "d", Orden = 1, CategoriaId = c2.Id });

        Assert.True(result.Success);
        Assert.Equal(c2.Nombre, (await Svc.GetAllModulesAsync(false)).Data!.Single(m => m.Id == moving.Id).CategoriaNombre);
        Assert.Equal(2, (await _host.Context.LearningModules.AsNoTracking().SingleAsync(m => m.Id == existing.Id)).Orden);
    }

    [Fact]
    public async Task Delete_Inexistente_Falla()
    {
        Assert.False((await Svc.DeleteModuleAsync(Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task Delete_ConActividades_Falla()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var module = await _host.CreateModuleAsync();
        await _host.CreateActivityAsync(module, teacher);

        var result = await Svc.DeleteModuleAsync(module.Id);

        Assert.False(result.Success);
        Assert.Contains("actividades activas", result.Message);
    }

    [Fact]
    public async Task Delete_SinCategoria_Elimina()
    {
        var module = await _host.CreateModuleAsync();

        Assert.True((await Svc.DeleteModuleAsync(module.Id)).Success);
        Assert.Equal(0, await _host.Context.LearningModules.CountAsync());
    }

    [Fact]
    public async Task Delete_ConCategoria_RecompactaOrdenesPosteriores()
    {
        var category = await _host.CreateCategoryAsync();
        var a = new LearningModule { Titulo = "A", Orden = 1, CategoriaId = category.Id };
        var b = new LearningModule { Titulo = "B", Orden = 2, CategoriaId = category.Id };
        _host.Context.LearningModules.AddRange(a, b);
        await _host.Context.SaveChangesAsync();

        var result = await Svc.DeleteModuleAsync(a.Id);

        Assert.True(result.Success);
        Assert.Equal(1, (await _host.Context.LearningModules.AsNoTracking().SingleAsync(m => m.Id == b.Id)).Orden);
    }
}

public class StudentServiceTests : IDisposable
{
    private readonly AppTestHost _host = new();
    private IStudentService Svc => _host.Get<IStudentService>();

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task DailyRoutine_SinPerfil_Falla()
    {
        Assert.False((await Svc.GetDailyRoutineForStudentUserAsync(Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task DailyRoutine_OrdenaPorSecuenciaYExcluyeFuturas()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var (user, student) = await _host.CreateStudentAsync();
        var module = await _host.CreateModuleAsync();
        var a1 = await _host.CreateActivityAsync(module, teacher, "Segunda");
        var a2 = await _host.CreateActivityAsync(module, teacher, "Primera");
        var a3 = await _host.CreateActivityAsync(module, teacher, "Futura");
        await _host.CreateAssignmentAsync(a1, student, orden: 2);
        await _host.CreateAssignmentAsync(a2, student, orden: 1);
        await _host.CreateAssignmentAsync(a3, student, orden: 3, start: DateTime.UtcNow.Date.AddDays(5));

        var result = await Svc.GetDailyRoutineForStudentUserAsync(user.Id);

        Assert.True(result.Success);
        Assert.Equal(new[] { "Primera", "Segunda" }, result.Data!.Select(r => r.Titulo));
        Assert.Equal(module.Titulo, result.Data[0].ModuloTitulo);
    }

    [Fact]
    public async Task DailyRoutine_ConFechaObjetivo_IncluyeAsignacionesFuturasDeEsaFecha()
    {
        var (_, teacher) = await _host.CreateTeacherAsync();
        var (user, student) = await _host.CreateStudentAsync();
        var activity = await _host.CreateActivityAsync(await _host.CreateModuleAsync(), teacher, "Futura");
        await _host.CreateAssignmentAsync(activity, student, start: DateTime.UtcNow.Date.AddDays(5), due: DateTime.UtcNow.Date.AddDays(9));

        var result = await Svc.GetDailyRoutineForStudentUserAsync(user.Id, DateTime.UtcNow.Date.AddDays(6));

        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task Profile_ExistenteConAulaYAcudiente()
    {
        var (gu, g) = await _host.CreateGuardianAsync();
        var classroom = await _host.CreateClassroomAsync(name: "Aula Verde");
        var (user, student) = await _host.CreateStudentAsync(classroom, g);

        var result = await Svc.GetStudentProfileByUserIdAsync(user.Id);

        Assert.True(result.Success);
        Assert.Equal("Aula Verde", result.Data!.ClassroomNombre);
        Assert.Equal(gu.NombreCompleto, result.Data.GuardianNombre);
        Assert.Equal(student.CodigoEstudiante, result.Data.CodigoEstudiante);
    }

    [Fact]
    public async Task Profile_Inexistente_Falla()
    {
        Assert.False((await Svc.GetStudentProfileByUserIdAsync(Guid.NewGuid())).Success);
    }

    [Fact]
    public async Task GetAll_YPorAcudiente()
    {
        var (gu, g) = await _host.CreateGuardianAsync();
        await _host.CreateStudentAsync(guardian: g);
        await _host.CreateStudentAsync();
        await _host.CreateStudentAsync();

        var all = await Svc.GetAllStudentsAsync();
        var mine = await Svc.GetStudentsForGuardianUserAsync(gu.Id);
        var none = await Svc.GetStudentsForGuardianUserAsync(Guid.NewGuid());

        Assert.Equal(3, all.Data!.Count);
        Assert.Single(mine.Data!);
        Assert.Empty(none.Data!);
    }
}
