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
        Guid? currentUserId = null,
        bool isStaff = true,
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepo.GetStudentWithDetailsAsync(studentId, cancellationToken)
                      ?? await _studentRepo.GetByIdAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<SensoryPreferenceDto>.Fail($"No se encontró el estudiante con identificador {studentId}.");
        }

        if (currentUserId.HasValue && !isStaff)
        {
            bool isOwnStudent = student.UserId == currentUserId.Value;
            bool isLinkedGuardian = student.GuardianProfile != null && student.GuardianProfile.UserId == currentUserId.Value;
            if (!isOwnStudent && !isLinkedGuardian)
            {
                return ApiResponse<SensoryPreferenceDto>.Fail("No tiene autorización para consultar las preferencias sensoriales de este estudiante.");
            }
        }

        var pref = await _sensoryRepo.GetByStudentIdAsync(studentId, cancellationToken);
        if (pref == null)
        {
            // Inicializar por defecto si aún no existe
            pref = new SensoryPreference
            {
                StudentProfileId = studentId,
                PaletaColor = "PastelSuave",
                Tipografia = "Atkinson Hyperlegible",
                MayorEspacioEntreLetras = false,
                AudioActivo = true,
                VolumenAudio = 60,
                TextoAVoz = true,
                AnimacionesReducidas = true,
                NivelEscalaFuente = 2,
                ModoContraste = Core.Enums.ModoContraste.Estandar,
                OcultarTemporizadores = true,
                ModoTemporizador = "sin-temporizador",
                SegundosAvisoPrevio = 15
            };
            await _sensoryRepo.AddAsync(pref, cancellationToken);
        }

        var dto = MapToDto(pref);

        return ApiResponse<SensoryPreferenceDto>.Ok(dto, "Preferencias sensoriales recuperadas exitosamente");
    }

    public async Task<ApiResponse<SensoryPreferenceDto>> UpdateSensoryPreferencesAsync(
        Guid studentId, 
        UpdateSensoryPreferenceDto dto, 
        Guid? currentUserId = null,
        bool isAdmin = true,
        CancellationToken cancellationToken = default)
    {
        var student = await _studentRepo.GetStudentWithDetailsAsync(studentId, cancellationToken)
                      ?? await _studentRepo.GetByIdAsync(studentId, cancellationToken);
        if (student == null)
        {
            return ApiResponse<SensoryPreferenceDto>.Fail($"No se encontró el estudiante con identificador {studentId}.");
        }

        if (currentUserId.HasValue && !isAdmin)
        {
            if (student.GuardianProfile == null || student.GuardianProfile.UserId != currentUserId.Value)
            {
                return ApiResponse<SensoryPreferenceDto>.Fail("No tiene autorización para modificar las preferencias sensoriales de este estudiante.");
            }
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

        var resultDto = MapToDto(pref);

        return ApiResponse<SensoryPreferenceDto>.Ok(resultDto, "Preferencias sensoriales actualizadas con éxito");
    }

    private static SensoryPreferenceDto MapToDto(SensoryPreference pref)
    {
        var resolvedMode = pref.OcultarTemporizadores
            ? "sin-temporizador"
            : (!string.IsNullOrWhiteSpace(pref.ModoTemporizador) && pref.ModoTemporizador != "sin-temporizador"
                ? pref.ModoTemporizador
                : "tiempo-extendido");

        return new SensoryPreferenceDto
        {
            Id = pref.Id,
            StudentProfileId = pref.StudentProfileId,
            PaletaColor = pref.PaletaColor,
            Tipografia = pref.Tipografia,
            MayorEspacioEntreLetras = pref.MayorEspacioEntreLetras,
            AudioActivo = pref.AudioActivo,
            VolumenAudio = pref.VolumenAudio,
            TextoAVoz = pref.TextoAVoz,
            AnimacionesReducidas = pref.AnimacionesReducidas,
            NivelEscalaFuente = pref.NivelEscalaFuente,
            ModoContraste = pref.ModoContraste,
            OcultarTemporizadores = pref.OcultarTemporizadores,
            ModoTemporizador = resolvedMode,
            SegundosAvisoPrevio = Math.Clamp(pref.SegundosAvisoPrevio, 0, 60)
        };
    }

    private static void ApplyDtoToEntity(UpdateSensoryPreferenceDto dto, SensoryPreference entity)
    {
        entity.PaletaColor = dto.PaletaColor;
        entity.Tipografia = dto.Tipografia;
        entity.MayorEspacioEntreLetras = dto.MayorEspacioEntreLetras;
        entity.AudioActivo = dto.AudioActivo;
        if (dto.VolumenAudio.HasValue)
        {
            entity.VolumenAudio = dto.VolumenAudio.Value;
        }
        if (dto.TextoAVoz.HasValue)
        {
            entity.TextoAVoz = dto.TextoAVoz.Value;
        }
        entity.AnimacionesReducidas = dto.AnimacionesReducidas;
        entity.NivelEscalaFuente = dto.NivelEscalaFuente;
        entity.ModoContraste = dto.ModoContraste;

        if (!string.IsNullOrWhiteSpace(dto.ModoTemporizador))
        {
            var mode = dto.ModoTemporizador.Trim().ToLowerInvariant();
            entity.ModoTemporizador = mode;
            entity.OcultarTemporizadores = mode == "sin-temporizador";
        }
        else
        {
            entity.OcultarTemporizadores = dto.OcultarTemporizadores;
            entity.ModoTemporizador = dto.OcultarTemporizadores
                ? "sin-temporizador"
                : (entity.ModoTemporizador == "estandar" ? "estandar" : "tiempo-extendido");
        }

        if (dto.SegundosAvisoPrevio.HasValue)
        {
            entity.SegundosAvisoPrevio = Math.Clamp(dto.SegundosAvisoPrevio.Value, 0, 60);
        }
    }
}
