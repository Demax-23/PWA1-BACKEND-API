using TeaLearning.Core.Entities;
using TeaLearning.Data.Repositories;
using TeaLearning.Services.Common;
using TeaLearning.Services.DTOs.Sensory;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class SensoryPreferenceService : ISensoryPreferenceService
{
    private readonly ISensoryPreferenceRepository _sensoryRepo;
    private readonly IStudentRepository _studentRepo;

    public SensoryPreferenceService(
        ISensoryPreferenceRepository sensoryRepo, 
        IStudentRepository studentRepo)
    {
        _sensoryRepo = sensoryRepo;
        _studentRepo = studentRepo;
    }

    public async Task<ApiResponse<SensoryPreferenceDto>> GetSensoryPreferencesByStudentIdAsync(
        Guid studentId, 
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepo.GetByIdAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<SensoryPreferenceDto>.Fail($"No se encontró el estudiante con identificador {studentId}.");
        }

        var pref = await _sensoryRepo.GetByStudentIdAsync(studentId, cancellationToken);
        if (pref == null)
        {
            // Inicializar por defecto si aún no existe
            pref = new SensoryPreference
            {
                StudentProfileId = studentId,
                PaletaColor = "PastelSuave",
                Tipografia = "OpenDyslexic",
                AudioActivo = true,
                AnimacionesReducidas = true,
                NivelEscalaFuente = 2,
                ModoContraste = Core.Enums.ModoContraste.Estandar,
                OcultarTemporizadores = true
            };
            await _sensoryRepo.AddAsync(pref, cancellationToken);
        }

        var dto = new SensoryPreferenceDto
        {
            Id = pref.Id,
            StudentProfileId = pref.StudentProfileId,
            PaletaColor = pref.PaletaColor,
            Tipografia = pref.Tipografia,
            AudioActivo = pref.AudioActivo,
            AnimacionesReducidas = pref.AnimacionesReducidas,
            NivelEscalaFuente = pref.NivelEscalaFuente,
            ModoContraste = pref.ModoContraste,
            OcultarTemporizadores = pref.OcultarTemporizadores
        };

        return ApiResponse<SensoryPreferenceDto>.Ok(dto, "Preferencias sensoriales recuperadas exitosamente");
    }

    public async Task<ApiResponse<SensoryPreferenceDto>> UpdateSensoryPreferencesAsync(
        Guid studentId, 
        UpdateSensoryPreferenceDto dto, 
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepo.GetByIdAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<SensoryPreferenceDto>.Fail($"No se encontró el estudiante con identificador {studentId}.");
        }

        var pref = await _sensoryRepo.GetByStudentIdAsync(studentId, cancellationToken);
        if (pref == null)
        {
            pref = new SensoryPreference
            {
                StudentProfileId = studentId
            };
            ApplyDtoToEntity(dto, pref);
            await _sensoryRepo.AddAsync(pref, cancellationToken);
        }
        else
        {
            ApplyDtoToEntity(dto, pref);
            await _sensoryRepo.UpdateAsync(pref, cancellationToken);
        }

        var resultDto = new SensoryPreferenceDto
        {
            Id = pref.Id,
            StudentProfileId = pref.StudentProfileId,
            PaletaColor = pref.PaletaColor,
            Tipografia = pref.Tipografia,
            AudioActivo = pref.AudioActivo,
            AnimacionesReducidas = pref.AnimacionesReducidas,
            NivelEscalaFuente = pref.NivelEscalaFuente,
            ModoContraste = pref.ModoContraste,
            OcultarTemporizadores = pref.OcultarTemporizadores
        };

        return ApiResponse<SensoryPreferenceDto>.Ok(resultDto, "Preferencias sensoriales actualizadas con éxito");
    }

    private static void ApplyDtoToEntity(UpdateSensoryPreferenceDto dto, SensoryPreference entity)
    {
        entity.PaletaColor = dto.PaletaColor;
        entity.Tipografia = dto.Tipografia;
        entity.AudioActivo = dto.AudioActivo;
        entity.AnimacionesReducidas = dto.AnimacionesReducidas;
        entity.NivelEscalaFuente = dto.NivelEscalaFuente;
        entity.ModoContraste = dto.ModoContraste;
        entity.OcultarTemporizadores = dto.OcultarTemporizadores;
    }
}
