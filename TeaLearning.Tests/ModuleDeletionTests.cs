using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Services;

namespace TeaLearning.Tests;

public class ModuleDeletionTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly LearningModuleRepository _moduleRepository;
    private readonly ModuleService _moduleService;

    public ModuleDeletionTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "ModuleDeletionTestDb_" + Guid.NewGuid().ToString("N"))
            .Options;

        _context = new ApplicationDbContext(options);
        _moduleRepository = new LearningModuleRepository(_context);
        _moduleService = new ModuleService(_moduleRepository);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task DeleteModuleAsync_WhenModuleHasNoActivities_HardDeletesAndShiftsOrders()
    {
        // Arrange
        var catId = Guid.NewGuid();
        var cat = new LearningCategory
        {
            Id = catId,
            Nombre = "Matemáticas",
            Orden = 1,
            IsActive = true
        };
        _context.LearningCategories.Add(cat);

        var mod1 = new LearningModule
        {
            Id = Guid.NewGuid(),
            CategoriaId = catId,
            Titulo = "Módulo 1",
            Orden = 1,
            IsActive = true
        };
        var mod2 = new LearningModule
        {
            Id = Guid.NewGuid(),
            CategoriaId = catId,
            Titulo = "Módulo 2",
            Orden = 2,
            IsActive = true
        };
        var mod3 = new LearningModule
        {
            Id = Guid.NewGuid(),
            CategoriaId = catId,
            Titulo = "Módulo 3",
            Orden = 3,
            IsActive = true
        };

        _context.LearningModules.AddRange(mod1, mod2, mod3);
        await _context.SaveChangesAsync();

        // Act: Delete mod2 (which has 0 activities)
        var result = await _moduleService.DeleteModuleAsync(mod2.Id);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Módulo temático eliminado exitosamente", result.Message);

        // Verify mod2 is physically removed from the DB
        var deleted = await _context.LearningModules.FindAsync(mod2.Id);
        Assert.Null(deleted);

        // Verify mod1 and mod3 remaining count
        var remaining = await _context.LearningModules
            .Where(m => m.CategoriaId == catId)
            .OrderBy(m => m.Orden)
            .ToListAsync();

        Assert.Equal(2, remaining.Count);
        Assert.Equal(mod1.Id, remaining[0].Id);
        Assert.Equal(1, remaining[0].Orden);

        Assert.Equal(mod3.Id, remaining[1].Id);
        Assert.Equal(2, remaining[1].Orden); // mod3 shifted from 3 down to 2
    }

    [Fact]
    public async Task DeleteModuleAsync_WhenModuleHasActiveActivities_ReturnsErrorAndDoesNotDelete()
    {
        // Arrange
        var modId = Guid.NewGuid();
        var mod = new LearningModule
        {
            Id = modId,
            Titulo = "Módulo con actividades",
            Orden = 1,
            IsActive = true
        };
        var act = new Activity
        {
            Id = Guid.NewGuid(),
            ModuloId = modId,
            Titulo = "Actividad 1",
            IsActive = true
        };

        _context.LearningModules.Add(mod);
        _context.Activities.Add(act);
        await _context.SaveChangesAsync();

        // Act
        var result = await _moduleService.DeleteModuleAsync(modId);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("actividades activas vinculadas", result.Message);

        var stillExists = await _context.LearningModules.FindAsync(modId);
        Assert.NotNull(stillExists);
    }
}
