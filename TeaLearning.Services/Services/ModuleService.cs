using TeaLearning.Core.Entities;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Activities;
using TeaLearning.Services.DTOs.Modules;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class ModuleService : IModuleService
{
    private readonly ILearningModuleRepository _moduleRepository;

    public ModuleService(ILearningModuleRepository moduleRepository)
    {
        _moduleRepository = moduleRepository;
    }

    public async Task<ApiResponse<List<ModuleDto>>> GetAllModulesAsync(bool onlyActive = true, CancellationToken cancellationToken = default)
    {
        var modules = await _moduleRepository.GetAllWithActivitiesCountAsync(onlyActive, cancellationToken);

        var dtoList = modules.Select(m => new ModuleDto
        {
            Id = m.Id,
            Titulo = m.Titulo,
            Descripcion = m.Descripcion,
            Icono = m.Icono,
            Orden = m.Orden,
            CategoriaId = m.CategoriaId,
            CategoriaNombre = m.Categoria?.Nombre,
            TotalActividades = m.Activities.Count,
            IsActive = m.IsActive,
            CreatedAt = m.CreatedAt
        }).ToList();

        return ApiResponse<List<ModuleDto>>.Ok(dtoList, $"Se recuperaron {dtoList.Count} módulos temáticos");
    }

    public async Task<ApiResponse<ModuleDetailDto>> GetModuleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetWithActivitiesAsync(id, includeInactive: false, cancellationToken);
        if (module == null)
        {
            return ApiResponse<ModuleDetailDto>.Fail($"Módulo temático con ID {id} no encontrado.");
        }

        var dto = new ModuleDetailDto
        {
            Id = module.Id,
            Titulo = module.Titulo,
            Descripcion = module.Descripcion,
            Icono = module.Icono,
            Orden = module.Orden,
            IsActive = module.IsActive,
            Activities = module.Activities.Select(a => new ActivitySummaryDto
            {
                Id = a.Id,
                Titulo = a.Titulo,
                TipoActividad = a.TipoActividad.ToString(),
                ObjetivoPedagogico = a.ObjetivoPedagogico,
                ModuloId = a.ModuloId
            }).ToList()
        };

        return ApiResponse<ModuleDetailDto>.Ok(dto, "Detalle del módulo temático recuperado con éxito");
    }

    public async Task<ApiResponse<ModuleDto>> CreateModuleAsync(CreateModuleDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.CategoriaId.HasValue)
        {
            await _moduleRepository.ShiftOrdersAsync(dto.CategoriaId.Value, dto.Orden, 1, null, cancellationToken);
        }

        var module = new LearningModule
        {
            Titulo = dto.Titulo.Trim(),
            Descripcion = dto.Descripcion.Trim(),
            Icono = dto.Icono?.Trim(),
            Orden = dto.Orden,
            CategoriaId = dto.CategoriaId,
            IsActive = true
        };

        await _moduleRepository.AddAsync(module, cancellationToken);

        var resultDto = new ModuleDto
        {
            Id = module.Id,
            Titulo = module.Titulo,
            Descripcion = module.Descripcion,
            Icono = module.Icono,
            Orden = module.Orden,
            CategoriaId = module.CategoriaId,
            TotalActividades = 0,
            IsActive = module.IsActive,
            CreatedAt = module.CreatedAt
        };

        return ApiResponse<ModuleDto>.Ok(resultDto, "Módulo temático creado exitosamente");
    }

    public async Task<ApiResponse<ModuleDto>> UpdateModuleAsync(Guid id, UpdateModuleDto dto, CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetWithActivitiesAsync(id, includeInactive: true, cancellationToken);
        if (module == null)
        {
            return ApiResponse<ModuleDto>.Fail($"Módulo temático con ID {id} no encontrado.");
        }

        if (module.CategoriaId == dto.CategoriaId && dto.CategoriaId.HasValue && module.Orden != dto.Orden)
        {
            await _moduleRepository.ReorderOnUpdateAsync(dto.CategoriaId.Value, module.Orden, dto.Orden, module.Id, cancellationToken);
        }
        else if (module.CategoriaId != dto.CategoriaId && dto.CategoriaId.HasValue)
        {
            await _moduleRepository.ShiftOrdersAsync(dto.CategoriaId.Value, dto.Orden, 1, null, cancellationToken);
        }

        module.Titulo = dto.Titulo.Trim();
        module.Descripcion = dto.Descripcion.Trim();
        module.Icono = dto.Icono?.Trim();
        module.Orden = dto.Orden;
        module.CategoriaId = dto.CategoriaId;
        module.IsActive = dto.IsActive;

        await _moduleRepository.UpdateAsync(module, cancellationToken);

        var resultDto = new ModuleDto
        {
            Id = module.Id,
            Titulo = module.Titulo,
            Descripcion = module.Descripcion,
            Icono = module.Icono,
            Orden = module.Orden,
            CategoriaId = module.CategoriaId,
            CategoriaNombre = module.Categoria?.Nombre,
            TotalActividades = module.Activities?.Count ?? 0,
            IsActive = module.IsActive,
            CreatedAt = module.CreatedAt
        };

        return ApiResponse<ModuleDto>.Ok(resultDto, "Módulo temático actualizado exitosamente");
    }

    public async Task<ApiResponse<bool>> DeleteModuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var module = await _moduleRepository.GetWithActivitiesAsync(id, includeInactive: true, cancellationToken);
        if (module == null)
        {
            return ApiResponse<bool>.Fail($"Módulo temático con ID {id} no encontrado.");
        }

        if (module.Activities.Any())
        {
            return ApiResponse<bool>.Fail("No es posible eliminar el módulo porque tiene actividades activas vinculadas.");
        }

        var categoriaId = module.CategoriaId;
        var orden = module.Orden;

        await _moduleRepository.DeleteAsync(module, cancellationToken);

        if (categoriaId.HasValue)
        {
            await _moduleRepository.ShiftOrdersAsync(categoriaId.Value, orden + 1, -1, null, cancellationToken);
        }

        return ApiResponse<bool>.Ok(true, "Módulo temático eliminado exitosamente");
    }
}
