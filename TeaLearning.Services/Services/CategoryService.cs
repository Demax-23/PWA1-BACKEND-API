using TeaLearning.Core.Entities;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Categories;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class CategoryService : ICategoryService
{
    private readonly ILearningCategoryRepository _categoryRepository;

    public CategoryService(ILearningCategoryRepository categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    public async Task<ApiResponse<List<CategoryDto>>> GetAllCategoriesAsync(bool onlyActive = false, CancellationToken cancellationToken = default)
    {
        var categories = await _categoryRepository.GetAllWithModulesCountAsync(onlyActive, cancellationToken);

        var dtoList = categories.Select(c => new CategoryDto
        {
            Id = c.Id,
            Nombre = c.Nombre,
            Descripcion = c.Descripcion,
            Color = c.Color,
            Orden = c.Orden,
            TotalModulos = c.Modules.Count(m => m.IsActive),
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt
        }).ToList();

        return ApiResponse<List<CategoryDto>>.Ok(dtoList, $"Se recuperaron {dtoList.Count} categorías de aprendizaje");
    }

    public async Task<ApiResponse<CategoryDto>> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetWithModulesAsync(id, cancellationToken);
        if (category == null)
        {
            return ApiResponse<CategoryDto>.Fail($"Categoría con ID {id} no encontrada.");
        }

        var dto = new CategoryDto
        {
            Id = category.Id,
            Nombre = category.Nombre,
            Descripcion = category.Descripcion,
            Color = category.Color,
            Orden = category.Orden,
            TotalModulos = category.Modules.Count(m => m.IsActive),
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt
        };

        return ApiResponse<CategoryDto>.Ok(dto, "Detalle de categoría recuperado con éxito");
    }

    public async Task<ApiResponse<CategoryDto>> CreateCategoryAsync(CreateCategoryDto dto, CancellationToken cancellationToken = default)
    {
        var trimmedName = dto.Nombre.Trim();

        if (await _categoryRepository.ExistsByNameAsync(trimmedName, null, cancellationToken))
        {
            return ApiResponse<CategoryDto>.Fail($"Ya existe una categoría pedagógica con el nombre '{trimmedName}'.");
        }

        await _categoryRepository.ShiftOrdersAsync(dto.Orden, 1, null, cancellationToken);

        var category = new LearningCategory
        {
            Nombre = trimmedName,
            Descripcion = dto.Descripcion?.Trim() ?? string.Empty,
            Color = string.IsNullOrWhiteSpace(dto.Color) ? "#2F6E5C" : dto.Color.Trim(),
            Orden = dto.Orden,
            IsActive = dto.IsActive
        };

        await _categoryRepository.AddAsync(category, cancellationToken);

        var resultDto = new CategoryDto
        {
            Id = category.Id,
            Nombre = category.Nombre,
            Descripcion = category.Descripcion,
            Color = category.Color,
            Orden = category.Orden,
            TotalModulos = 0,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt
        };

        return ApiResponse<CategoryDto>.Ok(resultDto, $"Categoría '{category.Nombre}' creada exitosamente en la base de datos");
    }

    public async Task<ApiResponse<CategoryDto>> UpdateCategoryAsync(Guid id, UpdateCategoryDto dto, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetWithModulesAsync(id, cancellationToken);
        if (category == null)
        {
            return ApiResponse<CategoryDto>.Fail($"Categoría con ID {id} no encontrada.");
        }

        var trimmedName = dto.Nombre.Trim();
        if (await _categoryRepository.ExistsByNameAsync(trimmedName, id, cancellationToken))
        {
            return ApiResponse<CategoryDto>.Fail($"Ya existe otra categoría pedagógica con el nombre '{trimmedName}'.");
        }

        if (category.Orden != dto.Orden)
        {
            await _categoryRepository.ReorderOnUpdateAsync(category.Orden, dto.Orden, category.Id, cancellationToken);
        }

        category.Nombre = trimmedName;
        category.Descripcion = dto.Descripcion?.Trim() ?? string.Empty;
        category.Color = string.IsNullOrWhiteSpace(dto.Color) ? "#2F6E5C" : dto.Color.Trim();
        category.Orden = dto.Orden;
        category.IsActive = dto.IsActive;
        category.UpdatedAt = DateTime.UtcNow;

        await _categoryRepository.UpdateAsync(category, cancellationToken);

        var resultDto = new CategoryDto
        {
            Id = category.Id,
            Nombre = category.Nombre,
            Descripcion = category.Descripcion,
            Color = category.Color,
            Orden = category.Orden,
            TotalModulos = category.Modules.Count,
            IsActive = category.IsActive,
            CreatedAt = category.CreatedAt
        };

        return ApiResponse<CategoryDto>.Ok(resultDto, $"Categoría '{category.Nombre}' actualizada correctamente");
    }

    public async Task<ApiResponse<bool>> DeleteCategoryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);
        if (category == null)
        {
            return ApiResponse<bool>.Fail($"Categoría con ID {id} no encontrada.");
        }

        if (await _categoryRepository.HasAssociatedModulesAsync(id, onlyActive: true, cancellationToken))
        {
            return ApiResponse<bool>.Fail($"No es posible eliminar la categoría '{category.Nombre}' porque tiene módulos temáticos asociados. Reasigna o elimina los módulos primero.");
        }

        await _categoryRepository.DisassociateInactiveModulesAsync(id, cancellationToken);
        await _categoryRepository.DeleteAsync(category, cancellationToken);
        return ApiResponse<bool>.Ok(true, $"Categoría '{category.Nombre}' eliminada permanentemente de la base de datos.");
    }
}
