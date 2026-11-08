-- =================================================================================================
-- PROYECTO: TeaLearning / Sendero (PWA1-BACKEND-API)
-- MOTOR: Microsoft SQL Server (T-SQL)
-- CONTENIDO:
--   5 Triggers de Negocio (INSERT, UPDATE, DELETE)
--   5 Stored Procedures Complejos (Variables, Tablas Temporales, Ciclos WHILE, Transacciones)
-- =================================================================================================

-- =================================================================================================
-- SECCIÓN 1: 5 TRIGGERS DE NEGOCIO
-- =================================================================================================

-- -------------------------------------------------------------------------------------------------
-- TRIGGER 1: TR_ActivityExecutions_AfterInsert_SyncAndAlert
-- Evento: AFTER INSERT ON [dbo].[ActivityExecutions]
-- Propósito:
--   1. Sincroniza automáticamente el estado de la asignación en ActivityAssignments (Completada / EnProgreso).
--   2. Actualiza UltimaActividadAt del usuario estudiante en AspNetUsers.
--   3. Genera una alerta pedagógica automática en PedagogicalNotes si detecta indicadores de
--      frustración o sobrecarga sensorial (>= 4 fallos o >= 180 segundos sin completar).
-- -------------------------------------------------------------------------------------------------
CREATE OR ALTER TRIGGER [dbo].[TR_ActivityExecutions_AfterInsert_SyncAndAlert]
ON [dbo].[ActivityExecutions]
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AhoraUtc DATETIME2 = SYSUTCDATETIME();

    -- 1. Sincronizar estado de ActivityAssignments a 'Completada' cuando la ejecución fue exitosa
    UPDATE aa
    SET aa.Estado = N'Completada',
        aa.UpdatedAt = @AhoraUtc
    FROM [dbo].[ActivityAssignments] aa
    INNER JOIN inserted i ON i.AssignmentId = aa.Id
    WHERE i.CompletadaExitosamente = 1
      AND aa.Estado <> N'Completada';

    -- 2. Sincronizar estado de ActivityAssignments a 'EnProgreso' cuando estaba 'Pendiente' y no se completó
    UPDATE aa
    SET aa.Estado = N'EnProgreso',
        aa.UpdatedAt = @AhoraUtc
    FROM [dbo].[ActivityAssignments] aa
    INNER JOIN inserted i ON i.AssignmentId = aa.Id
    WHERE i.CompletadaExitosamente = 0
      AND aa.Estado = N'Pendiente'
      AND NOT EXISTS (
          SELECT 1
          FROM inserted i2
          WHERE i2.AssignmentId = aa.Id
            AND i2.CompletadaExitosamente = 1
      );

    -- 3. Actualizar UltimaActividadAt del usuario estudiante en AspNetUsers
    UPDATE u
    SET u.UltimaActividadAt = @AhoraUtc
    FROM [dbo].[AspNetUsers] u
    INNER JOIN [dbo].[StudentProfiles] sp ON sp.UserId = u.Id
    INNER JOIN inserted i ON i.StudentProfileId = sp.Id;

    -- 4. Detectar episodios de frustración o sobrecarga (CompletadaExitosamente = 0 y >= 4 fallos o >= 180s)
    INSERT INTO [dbo].[PedagogicalNotes] (
        [Id],
        [StudentProfileId],
        [TeacherProfileId],
        [AssignmentId],
        [FechaRegistro],
        [Area],
        [Tipo],
        [Titulo],
        [Contenido],
        [RecomendacionCasa],
        [VisibleAcudiente],
        [CreatedAt],
        [IsActive]
    )
    SELECT
        NEWID(),
        i.StudentProfileId,
        COALESCE(c.TeacherProfileId, act.DocenteId),
        i.AssignmentId,
        @AhoraUtc,
        N'Cognitiva',
        N'Alerta',
        N'[AUTO-TRIGGER] Alerta de frustración en actividad',
        CONCAT(
            N'Se detectó un intento no completado con indicadores de sobrecarga en la actividad "',
            ISNULL(act.Titulo, N'Actividad'),
            N'". Intentos fallidos registrados: ',
            CAST(i.CantidadIntentosFallidos AS NVARCHAR(10)),
            N', aciertos: ',
            CAST(i.CantidadAciertos AS NVARCHAR(10)),
            N', tiempo empleado: ',
            CAST(i.TiempoRespuestaSegundos AS NVARCHAR(10)),
            N' segundos.'
        ),
        N'Realizar pausa activa de regulación sensorial de 5 minutos, validar apoyos visuales y activar modo sin temporizador antes de reintentar.',
        0,
        @AhoraUtc,
        1
    FROM inserted i
    INNER JOIN [dbo].[ActivityAssignments] aa ON aa.Id = i.AssignmentId
    INNER JOIN [dbo].[Activities] act ON act.Id = aa.ActivityId
    INNER JOIN [dbo].[StudentProfiles] sp ON sp.Id = i.StudentProfileId
    LEFT JOIN [dbo].[Classrooms] c ON c.Id = sp.ClassroomId
    WHERE i.CompletadaExitosamente = 0
      AND (i.CantidadIntentosFallidos >= 4 OR i.TiempoRespuestaSegundos >= 180)
      AND COALESCE(c.TeacherProfileId, act.DocenteId) IS NOT NULL
      AND NOT EXISTS (
          SELECT 1
          FROM [dbo].[PedagogicalNotes] pn
          WHERE pn.StudentProfileId = i.StudentProfileId
            AND pn.AssignmentId = i.AssignmentId
            AND pn.Titulo = N'[AUTO-TRIGGER] Alerta de frustración en actividad'
            AND CAST(pn.FechaRegistro AS DATE) = CAST(@AhoraUtc AS DATE)
      );
END;
GO

-- -------------------------------------------------------------------------------------------------
-- TRIGGER 2: TR_ActivityAssignments_AfterUpdate_GradeAndRoutine
-- Evento: AFTER UPDATE ON [dbo].[ActivityAssignments]
-- Propósito:
--   1. Asegura Estado = 'Completada' y FechaCalificacion al registrar una calificación.
--   2. Crea automáticamente la nota pedagógica de retroalimentación (Alerta / Observacion / Logro).
--   3. Si Calificacion < 3.0, programa automáticamente una asignación de refuerzo con prioridad Alta.
--   4. Promueve la prioridad de la siguiente actividad pendiente en la rutina diaria del estudiante.
-- -------------------------------------------------------------------------------------------------
CREATE OR ALTER TRIGGER [dbo].[TR_ActivityAssignments_AfterUpdate_GradeAndRoutine]
ON [dbo].[ActivityAssignments]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF TRIGGER_NESTLEVEL() > 1
        RETURN;

    DECLARE @AhoraUtc DATETIME2 = SYSUTCDATETIME();

    UPDATE aa
    SET aa.Estado = N'Completada',
        aa.FechaCalificacion = ISNULL(i.FechaCalificacion, @AhoraUtc),
        aa.UpdatedAt = @AhoraUtc
    FROM [dbo].[ActivityAssignments] aa
    INNER JOIN inserted i ON i.Id = aa.Id
    INNER JOIN deleted d ON d.Id = i.Id
    WHERE i.Calificacion IS NOT NULL
      AND (d.Calificacion IS NULL OR d.Calificacion <> i.Calificacion)
      AND (aa.Estado <> N'Completada' OR aa.FechaCalificacion IS NULL);

    INSERT INTO [dbo].[PedagogicalNotes] (
        [Id], [StudentProfileId], [TeacherProfileId], [AssignmentId],
        [FechaRegistro], [Area], [Tipo], [Titulo], [Contenido],
        [RecomendacionCasa], [VisibleAcudiente], [CreatedAt], [IsActive]
    )
    SELECT
        NEWID(),
        i.StudentProfileId,
        COALESCE(i.CalificadoPorDocenteId, c.TeacherProfileId, act.DocenteId),
        i.Id,
        @AhoraUtc,
        CASE act.TipoActividad
            WHEN N'AsociacionEmocional' THEN N'Socioemocional'
            WHEN N'RutinaSocial' THEN N'Comunicacion'
            ELSE N'Cognitiva'
        END,
        CASE
            WHEN i.Calificacion < 3.0 THEN N'Alerta'
            WHEN i.Calificacion >= 4.5 THEN N'Logro'
            ELSE N'Observacion'
        END,
        CASE
            WHEN i.Calificacion < 3.0 THEN N'[AUTO-CALIFICACIÓN] Refuerzo requerido en actividad'
            WHEN i.Calificacion >= 4.5 THEN N'[AUTO-CALIFICACIÓN] Desempeño destacado en actividad'
            ELSE N'[AUTO-CALIFICACIÓN] Evaluación pedagógica registrada'
        END,
        CONCAT(
            N'Actividad evaluada: "', ISNULL(act.Titulo, N'Actividad'),
            N'" con calificación ', CAST(i.Calificacion AS NVARCHAR(10)), N'/5.0. ',
            ISNULL(i.ComentarioCalificacion, N'Se registró seguimiento pedagógico de la sesión.')
        ),
        CASE
            WHEN i.Calificacion < 3.0 THEN N'Repasar la secuencia con apoyos visuales paso a paso en casa sin presión de tiempo.'
            WHEN i.Calificacion >= 4.5 THEN N'Reconocer el logro con refuerzo positivo verbal y celebrar el avance en casa.'
            ELSE N'Continuar practicando la rutina en sesiones breves de 10 minutos.'
        END,
        1,
        @AhoraUtc,
        1
    FROM inserted i
    INNER JOIN deleted d ON d.Id = i.Id
    INNER JOIN [dbo].[Activities] act ON act.Id = i.ActivityId
    INNER JOIN [dbo].[StudentProfiles] sp ON sp.Id = i.StudentProfileId
    LEFT JOIN [dbo].[Classrooms] c ON c.Id = sp.ClassroomId
    WHERE i.Calificacion IS NOT NULL
      AND (d.Calificacion IS NULL OR d.Calificacion <> i.Calificacion)
      AND COALESCE(i.CalificadoPorDocenteId, c.TeacherProfileId, act.DocenteId) IS NOT NULL
      AND NOT EXISTS (
          SELECT 1
          FROM [dbo].[PedagogicalNotes] pn
          WHERE pn.AssignmentId = i.Id
            AND CHARINDEX(N'[AUTO-CALIFICACIÓN]', pn.Titulo) = 1
      );

    INSERT INTO [dbo].[ActivityAssignments] (
        [Id], [ActivityId], [StudentProfileId], [FechaInicio], [FechaLimite],
        [OrdenSecuencial], [Prioridad], [Estado], [CreatedAt], [IsActive]
    )
    SELECT
        NEWID(),
        i.ActivityId,
        i.StudentProfileId,
        DATEADD(DAY, 1, CAST(@AhoraUtc AS DATE)),
        DATEADD(DAY, 8, CAST(@AhoraUtc AS DATE)),
        ISNULL((
            SELECT MAX(aa2.OrdenSecuencial)
            FROM [dbo].[ActivityAssignments] aa2
            WHERE aa2.StudentProfileId = i.StudentProfileId
              AND CAST(aa2.FechaInicio AS DATE) = DATEADD(DAY, 1, CAST(@AhoraUtc AS DATE))
              AND aa2.IsActive = 1
        ), 0) + 1,
        N'Alta',
        N'Pendiente',
        @AhoraUtc,
        1
    FROM inserted i
    INNER JOIN deleted d ON d.Id = i.Id
    WHERE i.Calificacion IS NOT NULL
      AND i.Calificacion < 3.0
      AND (d.Calificacion IS NULL OR d.Calificacion >= 3.0)
      AND NOT EXISTS (
          SELECT 1
          FROM [dbo].[ActivityAssignments] existing
          WHERE existing.StudentProfileId = i.StudentProfileId
            AND existing.ActivityId = i.ActivityId
            AND existing.Id <> i.Id
            AND existing.Estado IN (N'Pendiente', N'EnProgreso')
            AND existing.IsActive = 1
      );

    ;WITH NextPending AS (
        SELECT
            aa.Id,
            ROW_NUMBER() OVER (
                PARTITION BY aa.StudentProfileId, CAST(aa.FechaInicio AS DATE)
                ORDER BY aa.OrdenSecuencial ASC
            ) AS Rn
        FROM [dbo].[ActivityAssignments] aa
        INNER JOIN inserted i ON i.StudentProfileId = aa.StudentProfileId
            AND CAST(i.FechaInicio AS DATE) = CAST(aa.FechaInicio AS DATE)
        INNER JOIN deleted d ON d.Id = i.Id
        WHERE i.Estado = N'Completada'
          AND d.Estado <> N'Completada'
          AND aa.Estado = N'Pendiente'
          AND aa.IsActive = 1
          AND aa.OrdenSecuencial > i.OrdenSecuencial
    )
    UPDATE aa
    SET aa.Prioridad = N'Media',
        aa.UpdatedAt = @AhoraUtc
    FROM [dbo].[ActivityAssignments] aa
    INNER JOIN NextPending np ON np.Id = aa.Id
    WHERE np.Rn = 1
      AND aa.Prioridad = N'Baja';
END;
GO

-- -------------------------------------------------------------------------------------------------
-- TRIGGER 3: TR_SensoryPreferences_AfterUpsert_EnforceAndLog
-- Evento: AFTER INSERT, UPDATE ON [dbo].[SensoryPreferences]
-- Propósito:
--   1. Garantiza invariantes de protección sensorial TEA (volumen, escala de fuente, aviso previo, temporizador).
--   2. Registra en PedagogicalNotes (Area = 'Sensorial') todo cambio en las preferencias sensoriales.
-- -------------------------------------------------------------------------------------------------
CREATE OR ALTER TRIGGER [dbo].[TR_SensoryPreferences_AfterUpsert_EnforceAndLog]
ON [dbo].[SensoryPreferences]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF TRIGGER_NESTLEVEL() > 1
        RETURN;

    DECLARE @AhoraUtc DATETIME2 = SYSUTCDATETIME();

    UPDATE spf
    SET spf.VolumenAudio = CASE
            WHEN i.AudioActivo = 0 THEN 0
            WHEN i.VolumenAudio < 0 THEN 0
            WHEN i.VolumenAudio > 100 THEN 100
            ELSE i.VolumenAudio
        END,
        spf.NivelEscalaFuente = CASE
            WHEN i.NivelEscalaFuente < 1 THEN 1
            WHEN i.NivelEscalaFuente > 3 THEN 3
            ELSE i.NivelEscalaFuente
        END,
        spf.SegundosAvisoPrevio = CASE
            WHEN i.SegundosAvisoPrevio < 5 THEN 5
            WHEN i.SegundosAvisoPrevio > 60 THEN 60
            ELSE i.SegundosAvisoPrevio
        END,
        spf.ModoTemporizador = CASE
            WHEN i.OcultarTemporizadores = 1 THEN N'sin-temporizador'
            ELSE i.ModoTemporizador
        END,
        spf.UpdatedAt = @AhoraUtc
    FROM [dbo].[SensoryPreferences] spf
    INNER JOIN inserted i ON i.Id = spf.Id
    WHERE (i.AudioActivo = 0 AND i.VolumenAudio <> 0)
       OR i.VolumenAudio < 0 OR i.VolumenAudio > 100
       OR i.NivelEscalaFuente < 1 OR i.NivelEscalaFuente > 3
       OR i.SegundosAvisoPrevio < 5 OR i.SegundosAvisoPrevio > 60
       OR (i.OcultarTemporizadores = 1 AND i.ModoTemporizador <> N'sin-temporizador');

    IF EXISTS (SELECT 1 FROM deleted)
    BEGIN
        INSERT INTO [dbo].[PedagogicalNotes] (
            [Id], [StudentProfileId], [TeacherProfileId], [AssignmentId],
            [FechaRegistro], [Area], [Tipo], [Titulo], [Contenido],
            [RecomendacionCasa], [VisibleAcudiente], [CreatedAt], [IsActive]
        )
        SELECT
            NEWID(),
            i.StudentProfileId,
            COALESCE(c.TeacherProfileId, (SELECT TOP 1 tp.Id FROM [dbo].[TeacherProfiles] tp WHERE tp.IsActive = 1 ORDER BY tp.CreatedAt)),
            NULL,
            @AhoraUtc,
            N'Sensorial',
            N'Observacion',
            N'[AUTO-SENSORIAL] Ajuste de perfil sensorial TEA',
            CONCAT(
                N'Se actualizó la configuración sensorial del estudiante. Paleta: ', i.PaletaColor,
                N', Contraste: ', i.ModoContraste,
                N', Audio: ', CASE WHEN i.AudioActivo = 1 THEN CONCAT(N'Activo (', CAST(i.VolumenAudio AS NVARCHAR(10)), N'%)') ELSE N'Silenciado' END,
                N', Temporizador: ', CASE WHEN i.OcultarTemporizadores = 1 THEN N'Oculto (sin presión)' ELSE i.ModoTemporizador END,
                N', Animaciones reducidas: ', CASE WHEN i.AnimacionesReducidas = 1 THEN N'Sí' ELSE N'No' END, N'.'
            ),
            N'Mantener las mismas preferencias visuales y auditivas durante las sesiones de acompañamiento en el hogar.',
            1,
            @AhoraUtc,
            1
        FROM inserted i
        INNER JOIN deleted d ON d.Id = i.Id
        INNER JOIN [dbo].[StudentProfiles] st ON st.Id = i.StudentProfileId
        LEFT JOIN [dbo].[Classrooms] c ON c.Id = st.ClassroomId
        WHERE (
            ISNULL(i.PaletaColor, N'') <> ISNULL(d.PaletaColor, N'')
            OR ISNULL(i.ModoContraste, N'') <> ISNULL(d.ModoContraste, N'')
            OR i.AudioActivo <> d.AudioActivo
            OR i.VolumenAudio <> d.VolumenAudio
            OR i.AnimacionesReducidas <> d.AnimacionesReducidas
            OR i.OcultarTemporizadores <> d.OcultarTemporizadores
            OR ISNULL(i.ModoTemporizador, N'') <> ISNULL(d.ModoTemporizador, N'')
            OR i.TextoAVoz <> d.TextoAVoz
        )
        AND COALESCE(c.TeacherProfileId, (SELECT TOP 1 tp.Id FROM [dbo].[TeacherProfiles] tp WHERE tp.IsActive = 1 ORDER BY tp.CreatedAt)) IS NOT NULL;
    END
END;
GO

-- -------------------------------------------------------------------------------------------------
-- TRIGGER 4: TR_Activities_AfterUpdate_CascadeDeactivation
-- Evento: AFTER UPDATE ON [dbo].[Activities]
-- Propósito:
--   Cuando una actividad pasa de activa a inactiva (IsActive 1 -> 0):
--   1. Desactiva asignaciones Pendientes/EnProgreso preservando las Completadas.
--   2. Recompacta OrdenSecuencial de la agenda diaria de los estudiantes afectados.
--   3. Inserta nota informativa en PedagogicalNotes para anticipar el cambio de rutina.
-- -------------------------------------------------------------------------------------------------
CREATE OR ALTER TRIGGER [dbo].[TR_Activities_AfterUpdate_CascadeDeactivation]
ON [dbo].[Activities]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (
        SELECT 1
        FROM inserted i
        INNER JOIN deleted d ON d.Id = i.Id
        WHERE d.IsActive = 1 AND i.IsActive = 0
    )
    BEGIN
        RETURN;
    END

    DECLARE @AhoraUtc DATETIME2 = SYSUTCDATETIME();

    DECLARE @AffectedAssignments TABLE (
        AssignmentId UNIQUEIDENTIFIER,
        StudentProfileId UNIQUEIDENTIFIER,
        ActivityId UNIQUEIDENTIFIER,
        ActivityTitle NVARCHAR(200),
        DocenteId UNIQUEIDENTIFIER,
        FechaDia DATE
    );

    INSERT INTO @AffectedAssignments (AssignmentId, StudentProfileId, ActivityId, ActivityTitle, DocenteId, FechaDia)
    SELECT
        aa.Id,
        aa.StudentProfileId,
        aa.ActivityId,
        i.Titulo,
        i.DocenteId,
        CAST(aa.FechaInicio AS DATE)
    FROM [dbo].[ActivityAssignments] aa
    INNER JOIN inserted i ON i.Id = aa.ActivityId
    INNER JOIN deleted d ON d.Id = i.Id
    WHERE d.IsActive = 1
      AND i.IsActive = 0
      AND aa.IsActive = 1
      AND aa.Estado IN (N'Pendiente', N'EnProgreso');

    UPDATE aa
    SET aa.IsActive = 0,
        aa.UpdatedAt = @AhoraUtc
    FROM [dbo].[ActivityAssignments] aa
    INNER JOIN @AffectedAssignments aff ON aff.AssignmentId = aa.Id;

    ;WITH OrderedRemaining AS (
        SELECT
            aa.Id,
            ROW_NUMBER() OVER (
                PARTITION BY aa.StudentProfileId, CAST(aa.FechaInicio AS DATE)
                ORDER BY aa.OrdenSecuencial ASC, aa.CreatedAt ASC
            ) AS NuevoOrden
        FROM [dbo].[ActivityAssignments] aa
        WHERE aa.IsActive = 1
          AND EXISTS (
              SELECT 1
              FROM @AffectedAssignments aff
              WHERE aff.StudentProfileId = aa.StudentProfileId
                AND aff.FechaDia = CAST(aa.FechaInicio AS DATE)
          )
    )
    UPDATE aa
    SET aa.OrdenSecuencial = o.NuevoOrden,
        aa.UpdatedAt = @AhoraUtc
    FROM [dbo].[ActivityAssignments] aa
    INNER JOIN OrderedRemaining o ON o.Id = aa.Id
    WHERE aa.OrdenSecuencial <> o.NuevoOrden;

    INSERT INTO [dbo].[PedagogicalNotes] (
        [Id], [StudentProfileId], [TeacherProfileId], [AssignmentId],
        [FechaRegistro], [Area], [Tipo], [Titulo], [Contenido],
        [RecomendacionCasa], [VisibleAcudiente], [CreatedAt], [IsActive]
    )
    SELECT DISTINCT
        NEWID(),
        aff.StudentProfileId,
        COALESCE(c.TeacherProfileId, aff.DocenteId),
        NULL,
        @AhoraUtc,
        N'Conductual',
        N'Observacion',
        N'[AUTO-RUTINA] Ajuste de agenda por desactivación de actividad',
        CONCAT(
            N'La actividad "', aff.ActivityTitle,
            N'" fue retirada del catálogo activo y la agenda secuencial del estudiante fue reorganizada automáticamente sin espacios vacíos.'
        ),
        N'Anticipar verbalmente al estudiante el nuevo orden de su rutina visual del día para preservar la predictibilidad.',
        1,
        @AhoraUtc,
        1
    FROM @AffectedAssignments aff
    INNER JOIN [dbo].[StudentProfiles] sp ON sp.Id = aff.StudentProfileId
    LEFT JOIN [dbo].[Classrooms] c ON c.Id = sp.ClassroomId;
END;
GO

-- -------------------------------------------------------------------------------------------------
-- TRIGGER 5: TR_ActivityAssignments_AfterDelete_PreserveAndReorder
-- Evento: AFTER DELETE ON [dbo].[ActivityAssignments]
-- Propósito:
--   1. Preserva las notas pedagógicas asociadas desvinculando AssignmentId = NULL y añadiendo referencia histórica.
--   2. Recompacta OrdenSecuencial (1, 2, 3...) de las asignaciones activas restantes del estudiante en esa fecha.
-- -------------------------------------------------------------------------------------------------
CREATE OR ALTER TRIGGER [dbo].[TR_ActivityAssignments_AfterDelete_PreserveAndReorder]
ON [dbo].[ActivityAssignments]
AFTER DELETE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AhoraUtc DATETIME2 = SYSUTCDATETIME();

    UPDATE pn
    SET pn.AssignmentId = NULL,
        pn.Contenido = CASE
            WHEN CHARINDEX(N'[Ref: Asignación eliminada]', pn.Contenido) > 0 THEN pn.Contenido
            ELSE LEFT(CONCAT(pn.Contenido, N' [Ref: Asignación eliminada]'), 2000)
        END,
        pn.UpdatedAt = @AhoraUtc
    FROM [dbo].[PedagogicalNotes] pn
    INNER JOIN deleted d ON d.Id = pn.AssignmentId;

    ;WITH ReorderedRoutine AS (
        SELECT
            aa.Id,
            ROW_NUMBER() OVER (
                PARTITION BY aa.StudentProfileId, CAST(aa.FechaInicio AS DATE)
                ORDER BY aa.OrdenSecuencial ASC, aa.CreatedAt ASC
            ) AS NuevoOrden
        FROM [dbo].[ActivityAssignments] aa
        WHERE aa.IsActive = 1
          AND EXISTS (
              SELECT 1
              FROM deleted d
              WHERE d.StudentProfileId = aa.StudentProfileId
                AND CAST(d.FechaInicio AS DATE) = CAST(aa.FechaInicio AS DATE)
          )
    )
    UPDATE aa
    SET aa.OrdenSecuencial = r.NuevoOrden,
        aa.UpdatedAt = @AhoraUtc
    FROM [dbo].[ActivityAssignments] aa
    INNER JOIN ReorderedRoutine r ON r.Id = aa.Id
    WHERE aa.OrdenSecuencial <> r.NuevoOrden;
END;
GO

-- =================================================================================================
-- SECCIÓN 2: 5 STORED PROCEDURES COMPLEJOS DE NEGOCIO
-- =================================================================================================

-- -------------------------------------------------------------------------------------------------
-- STORED PROCEDURE 1: sp_GenerarReporteIntegralEstudiante
-- Incluye: Variables escalares, 3 tablas temporales (#SerieDiaria, #DesgloseTipoActividad, #ProgresoModulos),
--          2 ciclos WHILE (serie diaria con media móvil + evaluación por tipo de actividad),
--          cálculo de Índice de Progreso Pedagógico (IPP) y diagnóstico narrativo TEA.
-- -------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_GenerarReporteIntegralEstudiante]
    @StudentProfileId UNIQUEIDENTIFIER,
    @DiasVentana INT = 30,
    @SoloVisibleAcudiente BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF @DiasVentana IS NULL OR @DiasVentana < 7 SET @DiasVentana = 7;
    IF @DiasVentana > 180 SET @DiasVentana = 180;

    DECLARE @AhoraUtc DATETIME2 = SYSUTCDATETIME();
    DECLARE @FechaHoy DATE = CAST(@AhoraUtc AS DATE);
    DECLARE @FechaInicioVentana DATE = DATEADD(DAY, -(@DiasVentana - 1), @FechaHoy);

    DECLARE @CodigoEstudiante NVARCHAR(50);
    DECLARE @NombreCompleto NVARCHAR(220);
    DECLARE @FechaNacimiento DATETIME2;
    DECLARE @EdadAnios INT;
    DECLARE @AulaNombre NVARCHAR(100);
    DECLARE @AulaGrado NVARCHAR(50);
    DECLARE @DocenteNombre NVARCHAR(220);
    DECLARE @AcudienteNombre NVARCHAR(220);
    DECLARE @ObservacionesMedicasTEA NVARCHAR(1000);

    SELECT
        @CodigoEstudiante = sp.CodigoEstudiante,
        @NombreCompleto = LTRIM(RTRIM(ISNULL(u.Nombres, N'') + N' ' + ISNULL(u.Apellidos, N''))),
        @FechaNacimiento = sp.FechaNacimiento,
        @EdadAnios = DATEDIFF(YEAR, sp.FechaNacimiento, @FechaHoy) -
            CASE WHEN DATEADD(YEAR, DATEDIFF(YEAR, sp.FechaNacimiento, @FechaHoy), sp.FechaNacimiento) > @FechaHoy THEN 1 ELSE 0 END,
        @AulaNombre = ISNULL(c.Nombre, N'Sin aula asignada'),
        @AulaGrado = ISNULL(c.Grado, N'General'),
        @DocenteNombre = LTRIM(RTRIM(ISNULL(tu.Nombres, N'') + N' ' + ISNULL(tu.Apellidos, N''))),
        @AcudienteNombre = LTRIM(RTRIM(ISNULL(gu.Nombres, N'') + N' ' + ISNULL(gu.Apellidos, N''))),
        @ObservacionesMedicasTEA = ISNULL(sp.ObservacionesMedicasTEA, N'Sin observaciones clínicas registradas.')
    FROM [dbo].[StudentProfiles] sp
    INNER JOIN [dbo].[AspNetUsers] u ON u.Id = sp.UserId
    LEFT JOIN [dbo].[Classrooms] c ON c.Id = sp.ClassroomId
    LEFT JOIN [dbo].[TeacherProfiles] tp ON tp.Id = c.TeacherProfileId
    LEFT JOIN [dbo].[AspNetUsers] tu ON tu.Id = tp.UserId
    LEFT JOIN [dbo].[GuardianProfiles] gp ON gp.Id = sp.GuardianProfileId
    LEFT JOIN [dbo].[AspNetUsers] gu ON gu.Id = gp.UserId
    WHERE sp.Id = @StudentProfileId;

    CREATE TABLE #SerieDiaria (
        DiaIndice INT NOT NULL PRIMARY KEY,
        Fecha DATE NOT NULL,
        EtiquetaFecha NVARCHAR(20) NOT NULL,
        TotalEjecuciones INT NOT NULL,
        Completadas INT NOT NULL,
        Aciertos INT NOT NULL,
        Fallos INT NOT NULL,
        TiempoPromedioSeg INT NOT NULL,
        TasaExitoDia DECIMAL(5,2) NOT NULL,
        PrecisionDia DECIMAL(5,2) NOT NULL,
        MediaMovilExito DECIMAL(5,2) NOT NULL
    );

    DECLARE @DiaCursor DATE = @FechaInicioVentana;
    DECLARE @Indice INT = 1;
    DECLARE @RachaActualDias INT = 0;
    DECLARE @MejorRachaDias INT = 0;

    WHILE @DiaCursor <= @FechaHoy
    BEGIN
        DECLARE @ExecDia INT = 0;
        DECLARE @CompDia INT = 0;
        DECLARE @AcDia INT = 0;
        DECLARE @FaDia INT = 0;
        DECLARE @TiempoDia INT = 0;
        DECLARE @TasaDia DECIMAL(5,2) = 0;
        DECLARE @PrecDia DECIMAL(5,2) = 0;
        DECLARE @MediaMovil DECIMAL(5,2) = 0;

        SELECT
            @ExecDia = COUNT(1),
            @CompDia = ISNULL(SUM(CASE WHEN ae.CompletadaExitosamente = 1 THEN 1 ELSE 0 END), 0),
            @AcDia = ISNULL(SUM(ae.CantidadAciertos), 0),
            @FaDia = ISNULL(SUM(ae.CantidadIntentosFallidos), 0),
            @TiempoDia = ISNULL(AVG(ae.TiempoRespuestaSegundos), 0)
        FROM [dbo].[ActivityExecutions] ae
        WHERE ae.StudentProfileId = @StudentProfileId
          AND ae.IsActive = 1
          AND CAST(ae.FechaRegistro AS DATE) = @DiaCursor;

        IF @ExecDia > 0
            SET @TasaDia = ROUND((CAST(@CompDia AS DECIMAL(10,2)) * 100.0) / @ExecDia, 2);

        IF (@AcDia + @FaDia) > 0
            SET @PrecDia = ROUND((CAST(@AcDia AS DECIMAL(10,2)) * 100.0) / (@AcDia + @FaDia), 2);

        IF @CompDia > 0
        BEGIN
            SET @RachaActualDias = @RachaActualDias + 1;
            IF @RachaActualDias > @MejorRachaDias
                SET @MejorRachaDias = @RachaActualDias;
        END
        ELSE IF @ExecDia > 0 AND @CompDia = 0
        BEGIN
            SET @RachaActualDias = 0;
        END

        INSERT INTO #SerieDiaria (
            DiaIndice, Fecha, EtiquetaFecha, TotalEjecuciones, Completadas,
            Aciertos, Fallos, TiempoPromedioSeg, TasaExitoDia, PrecisionDia, MediaMovilExito
        )
        VALUES (
            @Indice, @DiaCursor, FORMAT(@DiaCursor, 'dd/MM'),
            @ExecDia, @CompDia, @AcDia, @FaDia, @TiempoDia, @TasaDia, @PrecDia, 0
        );

        SELECT @MediaMovil = ISNULL(ROUND(AVG(s.TasaExitoDia), 2), @TasaDia)
        FROM #SerieDiaria s
        WHERE s.DiaIndice BETWEEN (@Indice - 6) AND @Indice
          AND s.TotalEjecuciones > 0;

        UPDATE #SerieDiaria SET MediaMovilExito = @MediaMovil WHERE DiaIndice = @Indice;

        SET @DiaCursor = DATEADD(DAY, 1, @DiaCursor);
        SET @Indice = @Indice + 1;
    END;

    CREATE TABLE #TiposCatalogo (
        RowNum INT NOT NULL PRIMARY KEY,
        CodigoTipo NVARCHAR(50) NOT NULL,
        EtiquetaTipo NVARCHAR(80) NOT NULL
    );

    INSERT INTO #TiposCatalogo (RowNum, CodigoTipo, EtiquetaTipo)
    VALUES
        (1, N'SecuenciaLogica', N'Secuencia Lógica'),
        (2, N'EmparejamientoVisual', N'Emparejamiento Visual'),
        (3, N'AsociacionEmocional', N'Asociación Emocional'),
        (4, N'RutinaSocial', N'Rutina Social');

    CREATE TABLE #DesgloseTipoActividad (
        RowNum INT NOT NULL PRIMARY KEY,
        TipoActividad NVARCHAR(50) NOT NULL,
        EtiquetaTipo NVARCHAR(80) NOT NULL,
        TotalAsignaciones INT NOT NULL,
        TotalEjecuciones INT NOT NULL,
        Completadas INT NOT NULL,
        TasaExito DECIMAL(5,2) NOT NULL,
        PromedioAciertos DECIMAL(6,2) NOT NULL,
        PromedioFallos DECIMAL(6,2) NOT NULL,
        TiempoPromedioSeg INT NOT NULL,
        CalificacionPromedio DECIMAL(4,2) NULL,
        NivelDominio NVARCHAR(40) NOT NULL
    );

    DECLARE @TipoIdx INT = 1;
    WHILE @TipoIdx <= 4
    BEGIN
        DECLARE @CodTipo NVARCHAR(50);
        DECLARE @EtiqTipo NVARCHAR(80);
        SELECT @CodTipo = CodigoTipo, @EtiqTipo = EtiquetaTipo FROM #TiposCatalogo WHERE RowNum = @TipoIdx;

        DECLARE @TotAsigTipo INT = 0;
        DECLARE @TotExecTipo INT = 0;
        DECLARE @CompTipo INT = 0;
        DECLARE @TasaTipo DECIMAL(5,2) = 0;
        DECLARE @PromAcTipo DECIMAL(6,2) = 0;
        DECLARE @PromFaTipo DECIMAL(6,2) = 0;
        DECLARE @TiempoTipo INT = 0;
        DECLARE @CalifTipo DECIMAL(4,2) = NULL;
        DECLARE @Dominio NVARCHAR(40) = N'Sin Datos';

        SELECT
            @TotAsigTipo = COUNT(1),
            @CalifTipo = ROUND(AVG(CAST(aa.Calificacion AS DECIMAL(5,2))), 2)
        FROM [dbo].[ActivityAssignments] aa
        INNER JOIN [dbo].[Activities] a ON a.Id = aa.ActivityId
        WHERE aa.StudentProfileId = @StudentProfileId
          AND aa.IsActive = 1
          AND a.TipoActividad = @CodTipo;

        SELECT
            @TotExecTipo = COUNT(1),
            @CompTipo = ISNULL(SUM(CASE WHEN ae.CompletadaExitosamente = 1 THEN 1 ELSE 0 END), 0),
            @PromAcTipo = ISNULL(ROUND(AVG(CAST(ae.CantidadAciertos AS DECIMAL(8,2))), 2), 0),
            @PromFaTipo = ISNULL(ROUND(AVG(CAST(ae.CantidadIntentosFallidos AS DECIMAL(8,2))), 2), 0),
            @TiempoTipo = ISNULL(AVG(ae.TiempoRespuestaSegundos), 0)
        FROM [dbo].[ActivityExecutions] ae
        INNER JOIN [dbo].[ActivityAssignments] aa ON aa.Id = ae.AssignmentId
        INNER JOIN [dbo].[Activities] a ON a.Id = aa.ActivityId
        WHERE ae.StudentProfileId = @StudentProfileId
          AND ae.IsActive = 1
          AND a.TipoActividad = @CodTipo;

        IF @TotExecTipo > 0
        BEGIN
            SET @TasaTipo = ROUND((CAST(@CompTipo AS DECIMAL(10,2)) * 100.0) / @TotExecTipo, 2);
            SET @Dominio = CASE
                WHEN @TasaTipo >= 80.0 AND @PromFaTipo <= 2.0 THEN N'Dominio Alto'
                WHEN @TasaTipo >= 55.0 THEN N'En Consolidación'
                ELSE N'Requiere Apoyo Visual'
            END;
        END

        INSERT INTO #DesgloseTipoActividad (
            RowNum, TipoActividad, EtiquetaTipo, TotalAsignaciones, TotalEjecuciones,
            Completadas, TasaExito, PromedioAciertos, PromedioFallos, TiempoPromedioSeg,
            CalificacionPromedio, NivelDominio
        )
        VALUES (
            @TipoIdx, @CodTipo, @EtiqTipo, @TotAsigTipo, @TotExecTipo,
            @CompTipo, @TasaTipo, @PromAcTipo, @PromFaTipo, @TiempoTipo,
            @CalifTipo, @Dominio
        );

        SET @TipoIdx = @TipoIdx + 1;
    END;

    CREATE TABLE #ProgresoModulos (
        ModuloId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        Titulo NVARCHAR(150) NOT NULL,
        Categoria NVARCHAR(150) NOT NULL,
        Orden INT NOT NULL,
        ActividadesTotales INT NOT NULL,
        ActividadesCompletadas INT NOT NULL,
        PorcentajeAvance INT NOT NULL,
        Completado BIT NOT NULL
    );

    INSERT INTO #ProgresoModulos (
        ModuloId, Titulo, Categoria, Orden, ActividadesTotales,
        ActividadesCompletadas, PorcentajeAvance, Completado
    )
    SELECT
        lm.Id,
        lm.Titulo,
        ISNULL(lc.Nombre, N'General'),
        lm.Orden,
        COUNT(DISTINCT a.Id),
        COUNT(DISTINCT CASE WHEN aa.Estado = N'Completada' OR ae.CompletadaExitosamente = 1 THEN a.Id ELSE NULL END),
        CASE
            WHEN COUNT(DISTINCT a.Id) = 0 THEN 0
            ELSE CAST(ROUND((CAST(COUNT(DISTINCT CASE WHEN aa.Estado = N'Completada' OR ae.CompletadaExitosamente = 1 THEN a.Id ELSE NULL END) AS DECIMAL(10,2)) * 100.0) / COUNT(DISTINCT a.Id), 0) AS INT)
        END,
        CASE
            WHEN COUNT(DISTINCT a.Id) > 0
             AND COUNT(DISTINCT CASE WHEN aa.Estado = N'Completada' OR ae.CompletadaExitosamente = 1 THEN a.Id ELSE NULL END) >= COUNT(DISTINCT a.Id)
            THEN 1 ELSE 0
        END
    FROM [dbo].[LearningModules] lm
    LEFT JOIN [dbo].[LearningCategories] lc ON lc.Id = lm.CategoriaId
    LEFT JOIN [dbo].[Activities] a ON a.ModuloId = lm.Id AND a.IsActive = 1
    LEFT JOIN [dbo].[ActivityAssignments] aa ON aa.ActivityId = a.Id AND aa.StudentProfileId = @StudentProfileId AND aa.IsActive = 1
    LEFT JOIN [dbo].[ActivityExecutions] ae ON ae.AssignmentId = aa.Id AND ae.StudentProfileId = @StudentProfileId AND ae.IsActive = 1
    WHERE lm.IsActive = 1
    GROUP BY lm.Id, lm.Titulo, lc.Nombre, lm.Orden;

    DECLARE @TotalAsignaciones INT = 0, @AsignacionesCompletadas INT = 0, @AsignacionesEnProgreso INT = 0, @AsignacionesPendientes INT = 0;
    DECLARE @CalificacionPromedio DECIMAL(4,2) = NULL;

    SELECT
        @TotalAsignaciones = COUNT(1),
        @AsignacionesCompletadas = ISNULL(SUM(CASE WHEN aa.Estado = N'Completada' THEN 1 ELSE 0 END), 0),
        @AsignacionesEnProgreso = ISNULL(SUM(CASE WHEN aa.Estado = N'EnProgreso' THEN 1 ELSE 0 END), 0),
        @AsignacionesPendientes = ISNULL(SUM(CASE WHEN aa.Estado = N'Pendiente' THEN 1 ELSE 0 END), 0),
        @CalificacionPromedio = ROUND(AVG(CAST(aa.Calificacion AS DECIMAL(5,2))), 2)
    FROM [dbo].[ActivityAssignments] aa
    WHERE aa.StudentProfileId = @StudentProfileId AND aa.IsActive = 1;

    DECLARE @TotalEjecuciones INT = 0, @EjecucionesExitosas INT = 0, @TotalAciertos INT = 0, @TotalFallos INT = 0, @TiempoPromedioSegundos INT = 0;
    DECLARE @TasaExitoGeneral DECIMAL(5,2) = 0, @PrecisionGeneral DECIMAL(5,2) = 0;

    SELECT
        @TotalEjecuciones = COUNT(1),
        @EjecucionesExitosas = ISNULL(SUM(CASE WHEN ae.CompletadaExitosamente = 1 THEN 1 ELSE 0 END), 0),
        @TotalAciertos = ISNULL(SUM(ae.CantidadAciertos), 0),
        @TotalFallos = ISNULL(SUM(ae.CantidadIntentosFallidos), 0),
        @TiempoPromedioSegundos = ISNULL(AVG(ae.TiempoRespuestaSegundos), 0)
    FROM [dbo].[ActivityExecutions] ae
    WHERE ae.StudentProfileId = @StudentProfileId AND ae.IsActive = 1;

    IF @TotalEjecuciones > 0
        SET @TasaExitoGeneral = ROUND((CAST(@EjecucionesExitosas AS DECIMAL(10,2)) * 100.0) / @TotalEjecuciones, 2);

    IF (@TotalAciertos + @TotalFallos) > 0
        SET @PrecisionGeneral = ROUND((CAST(@TotalAciertos AS DECIMAL(10,2)) * 100.0) / (@TotalAciertos + @TotalFallos), 2);

    DECLARE @MitadIndice INT = @DiasVentana / 2;
    DECLARE @PromPrimeraMitad DECIMAL(5,2) = ISNULL((SELECT AVG(TasaExitoDia) FROM #SerieDiaria WHERE DiaIndice <= @MitadIndice AND TotalEjecuciones > 0), 0);
    DECLARE @PromSegundaMitad DECIMAL(5,2) = ISNULL((SELECT AVG(TasaExitoDia) FROM #SerieDiaria WHERE DiaIndice > @MitadIndice AND TotalEjecuciones > 0), 0);
    DECLARE @TendenciaGeneral NVARCHAR(40) = CASE
        WHEN (@PromSegundaMitad - @PromPrimeraMitad) >= 8.0 THEN N'En Ascenso'
        WHEN (@PromPrimeraMitad - @PromSegundaMitad) >= 8.0 THEN N'Requiere Refuerzo'
        ELSE N'Estable'
    END;

    DECLARE @AvanceModulosProm DECIMAL(5,2) = ISNULL((SELECT AVG(CAST(PorcentajeAvance AS DECIMAL(5,2))) FROM #ProgresoModulos), 0);
    DECLARE @NotaNormalizada DECIMAL(5,2) = CASE WHEN @CalificacionPromedio IS NOT NULL THEN (@CalificacionPromedio * 20.0) ELSE @TasaExitoGeneral END;
    DECLARE @IndiceProgresoPedagogico DECIMAL(5,2) = ROUND(
        (@TasaExitoGeneral * 0.40) + (@PrecisionGeneral * 0.30) + (@AvanceModulosProm * 0.15) + (@NotaNormalizada * 0.15),
        2
    );

    DECLARE @DiagnosticoPedagogico NVARCHAR(1000) = CASE
        WHEN @TotalEjecuciones = 0 THEN N'El estudiante aún no registra ejecuciones en el periodo evaluado. Se sugiere iniciar con actividades de emparejamiento visual de baja carga cognitiva.'
        WHEN @IndiceProgresoPedagogico >= 75.0 THEN CONCAT(N'Evolución sobresaliente (IPP ', CAST(@IndiceProgresoPedagogico AS NVARCHAR(10)), N'/100). Demuestra alta comprensión de instrucciones secuenciales y autorregulación sostenida con una precisión del ', CAST(@PrecisionGeneral AS NVARCHAR(10)), N'%.')
        WHEN @IndiceProgresoPedagogico >= 50.0 THEN CONCAT(N'Progreso constante en consolidación (IPP ', CAST(@IndiceProgresoPedagogico AS NVARCHAR(10)), N'/100). Se evidencia respuesta favorable a la estructura predecible, con oportunidad de fortalecimiento en actividades donde registra ', CAST(@TotalFallos AS NVARCHAR(10)), N' intentos fallidos acumulados.')
        ELSE CONCAT(N'Se identifica necesidad de acompañamiento focalizado (IPP ', CAST(@IndiceProgresoPedagogico AS NVARCHAR(10)), N'/100). Se recomienda fragmentar las secuencias en pasos más cortos y reforzar la anticipación visual antes de cada sesión.')
    END;

    DECLARE @RecomendacionSensorial NVARCHAR(600);
    SELECT
        @RecomendacionSensorial = CONCAT(
            N'Perfil activo: paleta ', ISNULL(sp.PaletaColor, N'PastelSuave'),
            N' con contraste ', ISNULL(sp.ModoContraste, N'Estandar'),
            N' y tipografía ', ISNULL(sp.Tipografia, N'Atkinson Hyperlegible'), N'. ',
            CASE
                WHEN @TiempoPromedioSegundos > 90 AND sp.OcultarTemporizadores = 0 THEN N'Se sugiere activar "Ocultar Temporizadores" para disminuir la presión por tiempo de respuesta.'
                WHEN sp.AudioActivo = 1 AND sp.VolumenAudio > 70 THEN N'Se recomienda moderar el volumen de audio por debajo del 60% para prevenir fatiga auditiva.'
                ELSE N'La configuración sensorial actual favorece un entorno calmado y predecible para el aprendizaje.'
            END
        )
    FROM [dbo].[StudentProfiles] st
    LEFT JOIN [dbo].[SensoryPreferences] sp ON sp.StudentProfileId = st.Id
    WHERE st.Id = @StudentProfileId;

    SELECT
        @StudentProfileId AS StudentProfileId,
        ISNULL(@CodigoEstudiante, N'N/A') AS CodigoEstudiante,
        ISNULL(@NombreCompleto, N'Estudiante') AS NombreCompleto,
        @FechaNacimiento AS FechaNacimiento,
        ISNULL(@EdadAnios, 0) AS EdadAnios,
        ISNULL(@AulaNombre, N'Sin aula') AS AulaNombre,
        ISNULL(@AulaGrado, N'General') AS AulaGrado,
        ISNULL(NULLIF(@DocenteNombre, N''), N'Docente Titular') AS DocenteNombre,
        ISNULL(NULLIF(@AcudienteNombre, N''), N'Acudiente Registrado') AS AcudienteNombre,
        ISNULL(@ObservacionesMedicasTEA, N'') AS ObservacionesMedicasTEA,
        @TotalAsignaciones AS TotalAsignaciones,
        @AsignacionesCompletadas AS AsignacionesCompletadas,
        @AsignacionesEnProgreso AS AsignacionesEnProgreso,
        @AsignacionesPendientes AS AsignacionesPendientes,
        @TotalEjecuciones AS TotalEjecuciones,
        @EjecucionesExitosas AS EjecucionesExitosas,
        @TotalAciertos AS TotalAciertos,
        @TotalFallos AS TotalFallos,
        @TasaExitoGeneral AS TasaExitoGeneral,
        @PrecisionGeneral AS PrecisionGeneral,
        @TiempoPromedioSegundos AS TiempoPromedioSegundos,
        @CalificacionPromedio AS CalificacionPromedio,
        @IndiceProgresoPedagogico AS IndiceProgresoPedagogico,
        @TendenciaGeneral AS TendenciaGeneral,
        @RachaActualDias AS RachaActualDias,
        @MejorRachaDias AS MejorRachaDias,
        ISNULL(spf.PaletaColor, N'PastelSuave') AS PaletaColor,
        ISNULL(spf.ModoContraste, N'Estandar') AS ModoContraste,
        ISNULL(spf.Tipografia, N'Atkinson Hyperlegible') AS Tipografia,
        ISNULL(spf.AudioActivo, 1) AS AudioActivo,
        ISNULL(spf.VolumenAudio, 50) AS VolumenAudio,
        ISNULL(spf.TextoAVoz, 1) AS TextoAVoz,
        ISNULL(spf.AnimacionesReducidas, 1) AS AnimacionesReducidas,
        ISNULL(spf.OcultarTemporizadores, 1) AS OcultarTemporizadores,
        ISNULL(spf.ModoTemporizador, N'sin-temporizador') AS ModoTemporizador,
        ISNULL(spf.NivelEscalaFuente, 2) AS NivelEscalaFuente,
        ISNULL(@DiagnosticoPedagogico, N'') AS DiagnosticoPedagogico,
        ISNULL(@RecomendacionSensorial, N'') AS RecomendacionSensorial,
        @AhoraUtc AS FechaGeneracionUtc
    FROM [dbo].[StudentProfiles] st
    LEFT JOIN [dbo].[SensoryPreferences] spf ON spf.StudentProfileId = st.Id
    WHERE st.Id = @StudentProfileId;

    SELECT DiaIndice, Fecha, EtiquetaFecha, TotalEjecuciones, Completadas, Aciertos, Fallos, TiempoPromedioSeg, TasaExitoDia, PrecisionDia, MediaMovilExito
    FROM #SerieDiaria ORDER BY DiaIndice ASC;

    SELECT RowNum, TipoActividad, EtiquetaTipo, TotalAsignaciones, TotalEjecuciones, Completadas, TasaExito, PromedioAciertos, PromedioFallos, TiempoPromedioSeg, CalificacionPromedio, NivelDominio
    FROM #DesgloseTipoActividad ORDER BY RowNum ASC;

    SELECT ModuloId, Titulo, Categoria, Orden, ActividadesTotales, ActividadesCompletadas, PorcentajeAvance, Completado
    FROM #ProgresoModulos ORDER BY Orden ASC, Titulo ASC;

    SELECT TOP 15
        aa.Id AS AssignmentId, a.Titulo AS ActividadTitulo, a.TipoActividad AS TipoActividad,
        ISNULL(lm.Titulo, N'General') AS ModuloTitulo, aa.Estado AS Estado, aa.Prioridad AS Prioridad,
        aa.Calificacion AS Calificacion, aa.ComentarioCalificacion AS ComentarioCalificacion,
        aa.FechaInicio AS FechaInicio, aa.FechaCalificacion AS FechaCalificacion
    FROM [dbo].[ActivityAssignments] aa
    INNER JOIN [dbo].[Activities] a ON a.Id = aa.ActivityId
    LEFT JOIN [dbo].[LearningModules] lm ON lm.Id = a.ModuloId
    WHERE aa.StudentProfileId = @StudentProfileId AND aa.IsActive = 1
    ORDER BY COALESCE(aa.FechaCalificacion, aa.UpdatedAt, aa.FechaInicio) DESC;

    SELECT TOP 15
        pn.Id AS NoteId, pn.FechaRegistro AS FechaRegistro, pn.Area AS Area, pn.Tipo AS Tipo,
        pn.Titulo AS Titulo, pn.Contenido AS Contenido, pn.RecomendacionCasa AS RecomendacionCasa,
        pn.VisibleAcudiente AS VisibleAcudiente,
        LTRIM(RTRIM(ISNULL(tu.Nombres, N'') + N' ' + ISNULL(tu.Apellidos, N''))) AS DocenteNombre
    FROM [dbo].[PedagogicalNotes] pn
    LEFT JOIN [dbo].[TeacherProfiles] tp ON tp.Id = pn.TeacherProfileId
    LEFT JOIN [dbo].[AspNetUsers] tu ON tu.Id = tp.UserId
    WHERE pn.StudentProfileId = @StudentProfileId AND pn.IsActive = 1
      AND (@SoloVisibleAcudiente = 0 OR pn.VisibleAcudiente = 1)
    ORDER BY pn.FechaRegistro DESC;
END;
GO

-- -------------------------------------------------------------------------------------------------
-- STORED PROCEDURE 2: sp_GenerarReporteConsolidadoAula
-- Incluye: Variables escalares, 3 tablas temporales (#EstudiantesAula, #EfectividadModulosAula, #SerieSemanalAula),
--          2 ciclos WHILE (clasificación de semáforo de riesgo por estudiante + tendencia de 4 semanas).
-- -------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_GenerarReporteConsolidadoAula]
    @ClassroomId UNIQUEIDENTIFIER = NULL,
    @TeacherUserId UNIQUEIDENTIFIER = NULL,
    @DiasVentana INT = 30
AS
BEGIN
    SET NOCOUNT ON;

    IF @DiasVentana IS NULL OR @DiasVentana < 7 SET @DiasVentana = 30;
    DECLARE @AhoraUtc DATETIME2 = SYSUTCDATETIME();
    DECLARE @FechaHoy DATE = CAST(@AhoraUtc AS DATE);

    IF @ClassroomId IS NULL AND @TeacherUserId IS NOT NULL
    BEGIN
        SELECT TOP 1 @ClassroomId = c.Id
        FROM [dbo].[Classrooms] c
        INNER JOIN [dbo].[TeacherProfiles] tp ON tp.Id = c.TeacherProfileId
        WHERE tp.UserId = @TeacherUserId AND c.IsActive = 1
        ORDER BY c.CreatedAt ASC;
    END

    IF @ClassroomId IS NULL
    BEGIN
        SELECT TOP 1 @ClassroomId = c.Id
        FROM [dbo].[Classrooms] c
        WHERE c.IsActive = 1
        ORDER BY c.CreatedAt ASC;
    END

    CREATE TABLE #EstudiantesAula (
        RowNum INT IDENTITY(1,1) PRIMARY KEY,
        StudentProfileId UNIQUEIDENTIFIER NOT NULL,
        CodigoEstudiante NVARCHAR(50) NOT NULL,
        NombreCompleto NVARCHAR(220) NOT NULL,
        TotalAsignaciones INT NOT NULL DEFAULT 0,
        AsignacionesCompletadas INT NOT NULL DEFAULT 0,
        TotalEjecuciones INT NOT NULL DEFAULT 0,
        EjecucionesExitosas INT NOT NULL DEFAULT 0,
        TasaExito DECIMAL(5,2) NOT NULL DEFAULT 0,
        PrecisionPromedio DECIMAL(5,2) NOT NULL DEFAULT 0,
        TiempoPromedioSeg INT NOT NULL DEFAULT 0,
        CalificacionPromedio DECIMAL(4,2) NULL,
        AvanceModulosPct INT NOT NULL DEFAULT 0,
        AlertasActivas INT NOT NULL DEFAULT 0,
        NivelRiesgo NVARCHAR(30) NOT NULL DEFAULT N'Estable',
        AccionSugerida NVARCHAR(250) NOT NULL DEFAULT N''
    );

    INSERT INTO #EstudiantesAula (StudentProfileId, CodigoEstudiante, NombreCompleto)
    SELECT
        sp.Id,
        sp.CodigoEstudiante,
        LTRIM(RTRIM(ISNULL(u.Nombres, N'') + N' ' + ISNULL(u.Apellidos, N'')))
    FROM [dbo].[StudentProfiles] sp
    INNER JOIN [dbo].[AspNetUsers] u ON u.Id = sp.UserId
    WHERE sp.IsActive = 1
      AND (@ClassroomId IS NULL OR sp.ClassroomId = @ClassroomId)
    ORDER BY sp.CodigoEstudiante ASC;

    DECLARE @Idx INT = 1;
    DECLARE @MaxIdx INT = (SELECT COUNT(1) FROM #EstudiantesAula);

    WHILE @Idx <= @MaxIdx
    BEGIN
        DECLARE @EstId UNIQUEIDENTIFIER = (SELECT StudentProfileId FROM #EstudiantesAula WHERE RowNum = @Idx);
        DECLARE @TotAsig INT = 0, @AsigComp INT = 0, @TotExec INT = 0, @ExecExito INT = 0, @SumAc INT = 0, @SumFa INT = 0, @TiempoProm INT = 0, @Alertas INT = 0, @ActividadesDistintasComp INT = 0;
        DECLARE @CalifProm DECIMAL(4,2) = NULL, @Tasa DECIMAL(5,2) = 0, @Prec DECIMAL(5,2) = 0;
        DECLARE @TotalActividadesCatalogo INT = (SELECT COUNT(1) FROM [dbo].[Activities] WHERE IsActive = 1);

        SELECT
            @TotAsig = COUNT(1),
            @AsigComp = ISNULL(SUM(CASE WHEN aa.Estado = N'Completada' THEN 1 ELSE 0 END), 0),
            @CalifProm = ROUND(AVG(CAST(aa.Calificacion AS DECIMAL(5,2))), 2)
        FROM [dbo].[ActivityAssignments] aa
        WHERE aa.StudentProfileId = @EstId AND aa.IsActive = 1;

        SELECT
            @TotExec = COUNT(1),
            @ExecExito = ISNULL(SUM(CASE WHEN ae.CompletadaExitosamente = 1 THEN 1 ELSE 0 END), 0),
            @SumAc = ISNULL(SUM(ae.CantidadAciertos), 0),
            @SumFa = ISNULL(SUM(ae.CantidadIntentosFallidos), 0),
            @TiempoProm = ISNULL(AVG(ae.TiempoRespuestaSegundos), 0)
        FROM [dbo].[ActivityExecutions] ae
        WHERE ae.StudentProfileId = @EstId AND ae.IsActive = 1;

        IF @TotExec > 0 SET @Tasa = ROUND((CAST(@ExecExito AS DECIMAL(10,2)) * 100.0) / @TotExec, 2);
        IF (@SumAc + @SumFa) > 0 SET @Prec = ROUND((CAST(@SumAc AS DECIMAL(10,2)) * 100.0) / (@SumAc + @SumFa), 2);

        SELECT @Alertas = COUNT(1)
        FROM [dbo].[PedagogicalNotes] pn
        WHERE pn.StudentProfileId = @EstId AND pn.IsActive = 1 AND pn.Tipo = N'Alerta';

        SELECT @ActividadesDistintasComp = COUNT(DISTINCT aa.ActivityId)
        FROM [dbo].[ActivityAssignments] aa
        LEFT JOIN [dbo].[ActivityExecutions] ae ON ae.AssignmentId = aa.Id AND ae.CompletadaExitosamente = 1
        WHERE aa.StudentProfileId = @EstId AND aa.IsActive = 1 AND (aa.Estado = N'Completada' OR ae.Id IS NOT NULL);

        DECLARE @AvancePct INT = CASE
            WHEN @TotalActividadesCatalogo = 0 THEN 0
            ELSE CAST(ROUND((CAST(@ActividadesDistintasComp AS DECIMAL(10,2)) * 100.0) / @TotalActividadesCatalogo, 0) AS INT)
        END;

        DECLARE @Riesgo NVARCHAR(30) = N'Estable';
        DECLARE @Accion NVARCHAR(250) = N'Continuar progresión secuencial del plan actual.';

        IF @TotExec = 0 OR (@Tasa < 50.0) OR (@CalifProm IS NOT NULL AND @CalifProm < 3.0)
        BEGIN
            SET @Riesgo = N'AlertaPrioritaria';
            SET @Accion = CASE
                WHEN @TotExec = 0 THEN N'Sin ejecuciones registradas: programar sesión guiada inicial con apoyo visual.'
                ELSE N'Reforzar con actividades de emparejamiento visual y activar modo sin temporizador.'
            END;
        END
        ELSE IF @Tasa < 75.0 OR @Alertas >= 2 OR @TiempoProm > 95
        BEGIN
            SET @Riesgo = N'Seguimiento';
            SET @Accion = N'Monitorear tiempos de respuesta y dosificar carga cognitiva por sesión.';
        END

        UPDATE #EstudiantesAula
        SET TotalAsignaciones = @TotAsig, AsignacionesCompletadas = @AsigComp,
            TotalEjecuciones = @TotExec, EjecucionesExitosas = @ExecExito,
            TasaExito = @Tasa, PrecisionPromedio = @Prec, TiempoPromedioSeg = @TiempoProm,
            CalificacionPromedio = @CalifProm, AvanceModulosPct = @AvancePct,
            AlertasActivas = @Alertas, NivelRiesgo = @Riesgo, AccionSugerida = @Accion
        WHERE RowNum = @Idx;

        SET @Idx = @Idx + 1;
    END;

    CREATE TABLE #EfectividadModulosAula (
        ModuloId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        ModuloTitulo NVARCHAR(150) NOT NULL,
        CategoriaNombre NVARCHAR(150) NOT NULL,
        Orden INT NOT NULL,
        TotalAsignaciones INT NOT NULL,
        TotalEjecuciones INT NOT NULL,
        EjecucionesExitosas INT NOT NULL,
        TasaExito DECIMAL(5,2) NOT NULL,
        TiempoPromedioSeg INT NOT NULL,
        CalificacionPromedio DECIMAL(4,2) NULL
    );

    INSERT INTO #EfectividadModulosAula (
        ModuloId, ModuloTitulo, CategoriaNombre, Orden, TotalAsignaciones,
        TotalEjecuciones, EjecucionesExitosas, TasaExito, TiempoPromedioSeg, CalificacionPromedio
    )
    SELECT
        lm.Id, lm.Titulo, ISNULL(lc.Nombre, N'General'), lm.Orden,
        COUNT(DISTINCT aa.Id), COUNT(ae.Id),
        ISNULL(SUM(CASE WHEN ae.CompletadaExitosamente = 1 THEN 1 ELSE 0 END), 0),
        CASE WHEN COUNT(ae.Id) = 0 THEN 0 ELSE ROUND((CAST(SUM(CASE WHEN ae.CompletadaExitosamente = 1 THEN 1 ELSE 0 END) AS DECIMAL(10,2)) * 100.0) / COUNT(ae.Id), 2) END,
        ISNULL(AVG(ae.TiempoRespuestaSegundos), 0),
        ROUND(AVG(CAST(aa.Calificacion AS DECIMAL(5,2))), 2)
    FROM [dbo].[LearningModules] lm
    LEFT JOIN [dbo].[LearningCategories] lc ON lc.Id = lm.CategoriaId
    LEFT JOIN [dbo].[Activities] a ON a.ModuloId = lm.Id AND a.IsActive = 1
    LEFT JOIN [dbo].[ActivityAssignments] aa ON aa.ActivityId = a.Id AND aa.IsActive = 1 AND aa.StudentProfileId IN (SELECT StudentProfileId FROM #EstudiantesAula)
    LEFT JOIN [dbo].[ActivityExecutions] ae ON ae.AssignmentId = aa.Id AND ae.IsActive = 1
    WHERE lm.IsActive = 1
    GROUP BY lm.Id, lm.Titulo, lc.Nombre, lm.Orden;

    CREATE TABLE #SerieSemanalAula (
        SemanaIndice INT NOT NULL PRIMARY KEY,
        EtiquetaSemana NVARCHAR(40) NOT NULL,
        FechaInicio DATE NOT NULL,
        FechaFin DATE NOT NULL,
        TotalEjecuciones INT NOT NULL,
        EjecucionesExitosas INT NOT NULL,
        TasaExito DECIMAL(5,2) NOT NULL,
        PromedioAciertos DECIMAL(6,2) NOT NULL,
        PromedioFallos DECIMAL(6,2) NOT NULL,
        TiempoPromedioSeg INT NOT NULL
    );

    DECLARE @SemIdx INT = 1;
    WHILE @SemIdx <= 4
    BEGIN
        DECLARE @FinSem DATE = DATEADD(DAY, -((4 - @SemIdx) * 7), @FechaHoy);
        DECLARE @IniSem DATE = DATEADD(DAY, -6, @FinSem);
        DECLARE @ExecSem INT = 0, @ExitoSem INT = 0, @TmpSem INT = 0;
        DECLARE @TasaSem DECIMAL(5,2) = 0, @AcSem DECIMAL(6,2) = 0, @FaSem DECIMAL(6,2) = 0;

        SELECT
            @ExecSem = COUNT(1),
            @ExitoSem = ISNULL(SUM(CASE WHEN ae.CompletadaExitosamente = 1 THEN 1 ELSE 0 END), 0),
            @AcSem = ISNULL(ROUND(AVG(CAST(ae.CantidadAciertos AS DECIMAL(8,2))), 2), 0),
            @FaSem = ISNULL(ROUND(AVG(CAST(ae.CantidadIntentosFallidos AS DECIMAL(8,2))), 2), 0),
            @TmpSem = ISNULL(AVG(ae.TiempoRespuestaSegundos), 0)
        FROM [dbo].[ActivityExecutions] ae
        WHERE ae.IsActive = 1
          AND ae.StudentProfileId IN (SELECT StudentProfileId FROM #EstudiantesAula)
          AND CAST(ae.FechaRegistro AS DATE) BETWEEN @IniSem AND @FinSem;

        IF @ExecSem > 0 SET @TasaSem = ROUND((CAST(@ExitoSem AS DECIMAL(10,2)) * 100.0) / @ExecSem, 2);

        INSERT INTO #SerieSemanalAula (SemanaIndice, EtiquetaSemana, FechaInicio, FechaFin, TotalEjecuciones, EjecucionesExitosas, TasaExito, PromedioAciertos, PromedioFallos, TiempoPromedioSeg)
        VALUES (@SemIdx, CONCAT(N'Sem ', CAST(@SemIdx AS NVARCHAR(5)), N' (', FORMAT(@IniSem, 'dd/MM'), N'-', FORMAT(@FinSem, 'dd/MM'), N')'), @IniSem, @FinSem, @ExecSem, @ExitoSem, @TasaSem, @AcSem, @FaSem, @TmpSem);

        SET @SemIdx = @SemIdx + 1;
    END;

    SELECT
        c.Id AS ClassroomId, c.Nombre AS AulaNombre, c.Grado AS Grado, c.AnioLectivo AS AnioLectivo,
        LTRIM(RTRIM(ISNULL(tu.Nombres, N'') + N' ' + ISNULL(tu.Apellidos, N''))) AS DocenteNombre,
        ISNULL(tp.Especialidad, N'Educación Especial y TEA') AS EspecialidadDocente,
        (SELECT COUNT(1) FROM #EstudiantesAula) AS TotalEstudiantes,
        (SELECT COUNT(1) FROM #EstudiantesAula WHERE TotalEjecuciones > 0) AS EstudiantesActivosPeriodo,
        (SELECT COUNT(1) FROM #EstudiantesAula WHERE NivelRiesgo = N'AlertaPrioritaria') AS EstudiantesEnAlerta,
        (SELECT COUNT(1) FROM #EstudiantesAula WHERE NivelRiesgo = N'Seguimiento') AS EstudiantesEnSeguimiento,
        (SELECT COUNT(1) FROM #EstudiantesAula WHERE NivelRiesgo = N'Estable') AS EstudiantesEstables,
        ISNULL((SELECT SUM(TotalAsignaciones) FROM #EstudiantesAula), 0) AS TotalAsignacionesAula,
        ISNULL((SELECT SUM(AsignacionesCompletadas) FROM #EstudiantesAula), 0) AS AsignacionesCompletadasAula,
        ISNULL((SELECT SUM(TotalEjecuciones) FROM #EstudiantesAula), 0) AS TotalEjecucionesAula,
        ISNULL((SELECT ROUND(AVG(TasaExito), 2) FROM #EstudiantesAula WHERE TotalEjecuciones > 0), 0) AS TasaExitoPromedioAula,
        ISNULL((SELECT ROUND(AVG(PrecisionPromedio), 2) FROM #EstudiantesAula WHERE TotalEjecuciones > 0), 0) AS PrecisionPromedioAula,
        ISNULL((SELECT AVG(TiempoPromedioSeg) FROM #EstudiantesAula WHERE TotalEjecuciones > 0), 0) AS TiempoPromedioSegundosAula,
        (SELECT ROUND(AVG(CalificacionPromedio), 2) FROM #EstudiantesAula WHERE CalificacionPromedio IS NOT NULL) AS CalificacionPromedioAula,
        CONCAT(
            N'El aula registra ', CAST((SELECT COUNT(1) FROM #EstudiantesAula WHERE NivelRiesgo = N'Estable') AS NVARCHAR(10)),
            N' estudiantes con evolución estable, ',
            CAST((SELECT COUNT(1) FROM #EstudiantesAula WHERE NivelRiesgo = N'Seguimiento') AS NVARCHAR(10)),
            N' en seguimiento preventivo y ',
            CAST((SELECT COUNT(1) FROM #EstudiantesAula WHERE NivelRiesgo = N'AlertaPrioritaria') AS NVARCHAR(10)),
            N' con prioridad de adaptación curricular.'
        ) AS SintesisEjecutivaAula,
        @AhoraUtc AS FechaGeneracionUtc
    FROM [dbo].[Classrooms] c
    LEFT JOIN [dbo].[TeacherProfiles] tp ON tp.Id = c.TeacherProfileId
    LEFT JOIN [dbo].[AspNetUsers] tu ON tu.Id = tp.UserId
    WHERE c.Id = @ClassroomId;

    SELECT StudentProfileId, CodigoEstudiante, NombreCompleto, TotalAsignaciones, AsignacionesCompletadas, TotalEjecuciones, EjecucionesExitosas, TasaExito, PrecisionPromedio, TiempoPromedioSeg, CalificacionPromedio, AvanceModulosPct, AlertasActivas, NivelRiesgo, AccionSugerida
    FROM #EstudiantesAula
    ORDER BY CASE NivelRiesgo WHEN N'AlertaPrioritaria' THEN 1 WHEN N'Seguimiento' THEN 2 ELSE 3 END, CodigoEstudiante ASC;

    SELECT ModuloId, ModuloTitulo, CategoriaNombre, Orden, TotalAsignaciones, TotalEjecuciones, EjecucionesExitosas, TasaExito, TiempoPromedioSeg, CalificacionPromedio
    FROM #EfectividadModulosAula ORDER BY Orden ASC, ModuloTitulo ASC;

    SELECT SemanaIndice, EtiquetaSemana, FechaInicio, FechaFin, TotalEjecuciones, EjecucionesExitosas, TasaExito, PromedioAciertos, PromedioFallos, TiempoPromedioSeg
    FROM #SerieSemanalAula ORDER BY SemanaIndice ASC;
END;
GO

-- -------------------------------------------------------------------------------------------------
-- STORED PROCEDURE 3: sp_AsignarRutinaAdaptativaPorAula
-- Incluye: Transacción explícita con TRY/CATCH, tablas temporales (#ColaEstudiantes, #ResumenAsignacionesCreadas),
--          variable tipo tabla (@Candidatas) y doble ciclo WHILE anidado (estudiantes -> actividades).
-- -------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_AsignarRutinaAdaptativaPorAula]
    @ClassroomId UNIQUEIDENTIFIER,
    @TeacherUserId UNIQUEIDENTIFIER,
    @FechaInicio DATETIME2,
    @MaxActividadesPorEstudiante INT = 3
AS
BEGIN
    SET NOCOUNT ON;

    IF @MaxActividadesPorEstudiante IS NULL OR @MaxActividadesPorEstudiante < 1 SET @MaxActividadesPorEstudiante = 1;
    IF @MaxActividadesPorEstudiante > 6 SET @MaxActividadesPorEstudiante = 6;

    DECLARE @AhoraUtc DATETIME2 = SYSUTCDATETIME();
    DECLARE @FechaBase DATE = CAST(ISNULL(@FechaInicio, @AhoraUtc) AS DATE);

    CREATE TABLE #ColaEstudiantes (
        RowNum INT IDENTITY(1,1) PRIMARY KEY,
        StudentProfileId UNIQUEIDENTIFIER NOT NULL,
        CodigoEstudiante NVARCHAR(50) NOT NULL,
        EstudianteNombre NVARCHAR(220) NOT NULL,
        OcultarTemporizadores BIT NOT NULL,
        ModoTemporizador NVARCHAR(30) NOT NULL
    );

    INSERT INTO #ColaEstudiantes (StudentProfileId, CodigoEstudiante, EstudianteNombre, OcultarTemporizadores, ModoTemporizador)
    SELECT
        sp.Id,
        sp.CodigoEstudiante,
        LTRIM(RTRIM(ISNULL(u.Nombres, N'') + N' ' + ISNULL(u.Apellidos, N''))),
        ISNULL(spf.OcultarTemporizadores, 1),
        ISNULL(spf.ModoTemporizador, N'sin-temporizador')
    FROM [dbo].[StudentProfiles] sp
    INNER JOIN [dbo].[AspNetUsers] u ON u.Id = sp.UserId
    LEFT JOIN [dbo].[SensoryPreferences] spf ON spf.StudentProfileId = sp.Id
    WHERE sp.ClassroomId = @ClassroomId AND sp.IsActive = 1
    ORDER BY sp.CodigoEstudiante ASC;

    CREATE TABLE #ResumenAsignacionesCreadas (
        AssignmentId UNIQUEIDENTIFIER NOT NULL,
        StudentProfileId UNIQUEIDENTIFIER NOT NULL,
        CodigoEstudiante NVARCHAR(50) NOT NULL,
        EstudianteNombre NVARCHAR(220) NOT NULL,
        ActivityId UNIQUEIDENTIFIER NOT NULL,
        ActividadTitulo NVARCHAR(200) NOT NULL,
        TipoActividad NVARCHAR(50) NOT NULL,
        Prioridad NVARCHAR(20) NOT NULL,
        OrdenSecuencial INT NOT NULL,
        FechaInicio DATETIME2 NOT NULL,
        FechaLimite DATETIME2 NOT NULL,
        MotivoAdaptativo NVARCHAR(300) NOT NULL
    );

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @IdxEst INT = 1;
        DECLARE @TotalEst INT = (SELECT COUNT(1) FROM #ColaEstudiantes);

        WHILE @IdxEst <= @TotalEst
        BEGIN
            DECLARE @EstId UNIQUEIDENTIFIER, @CodEst NVARCHAR(50), @NomEst NVARCHAR(220), @SinTemp BIT, @ModoTemp NVARCHAR(30);
            SELECT @EstId = StudentProfileId, @CodEst = CodigoEstudiante, @NomEst = EstudianteNombre, @SinTemp = OcultarTemporizadores, @ModoTemp = ModoTemporizador
            FROM #ColaEstudiantes WHERE RowNum = @IdxEst;

            DECLARE @TotalExecRecientes INT = 0, @FallosRecientes INT = 0, @AciertosRecientes INT = 0;
            DECLARE @TasaFallo DECIMAL(5,2) = 0, @TipoDebil NVARCHAR(50) = NULL;

            SELECT
                @TotalExecRecientes = COUNT(1),
                @FallosRecientes = ISNULL(SUM(ae.CantidadIntentosFallidos), 0),
                @AciertosRecientes = ISNULL(SUM(ae.CantidadAciertos), 0)
            FROM [dbo].[ActivityExecutions] ae
            WHERE ae.StudentProfileId = @EstId AND ae.IsActive = 1 AND ae.FechaRegistro >= DATEADD(DAY, -14, @AhoraUtc);

            IF (@AciertosRecientes + @FallosRecientes) > 0
                SET @TasaFallo = ROUND((CAST(@FallosRecientes AS DECIMAL(10,2)) * 100.0) / (@AciertosRecientes + @FallosRecientes), 2);

            SELECT TOP 1 @TipoDebil = a.TipoActividad
            FROM [dbo].[ActivityExecutions] ae
            INNER JOIN [dbo].[ActivityAssignments] aa ON aa.Id = ae.AssignmentId
            INNER JOIN [dbo].[Activities] a ON a.Id = aa.ActivityId
            WHERE ae.StudentProfileId = @EstId AND ae.IsActive = 1
            GROUP BY a.TipoActividad
            ORDER BY AVG(CAST(ae.CantidadIntentosFallidos AS DECIMAL(8,2))) DESC;

            DECLARE @OrdenActual INT = ISNULL((
                SELECT MAX(aa.OrdenSecuencial)
                FROM [dbo].[ActivityAssignments] aa
                WHERE aa.StudentProfileId = @EstId AND CAST(aa.FechaInicio AS DATE) = @FechaBase AND aa.IsActive = 1
            ), 0);

            DECLARE @Candidatas TABLE (
                CandRow INT IDENTITY(1,1),
                ActivityId UNIQUEIDENTIFIER,
                Titulo NVARCHAR(200),
                TipoActividad NVARCHAR(50),
                PrioridadSugerida NVARCHAR(20),
                Motivo NVARCHAR(300)
            );
            DELETE FROM @Candidatas;

            INSERT INTO @Candidatas (ActivityId, Titulo, TipoActividad, PrioridadSugerida, Motivo)
            SELECT TOP (@MaxActividadesPorEstudiante)
                a.Id, a.Titulo, a.TipoActividad,
                CASE WHEN @TasaFallo >= 40.0 AND a.TipoActividad = ISNULL(@TipoDebil, N'EmparejamientoVisual') THEN N'Alta' ELSE N'Media' END,
                CASE
                    WHEN @TasaFallo >= 40.0 AND a.TipoActividad = ISNULL(@TipoDebil, N'EmparejamientoVisual')
                        THEN CONCAT(N'Refuerzo adaptativo por tasa de error reciente (', CAST(@TasaFallo AS NVARCHAR(10)), N'%) en ', a.TipoActividad)
                    WHEN @TotalExecRecientes = 0 THEN N'Actividad inicial de estructuración de rutina predecible'
                    ELSE N'Progresión secuencial de módulo pedagógico'
                END
            FROM [dbo].[Activities] a
            INNER JOIN [dbo].[LearningModules] lm ON lm.Id = a.ModuloId
            WHERE a.IsActive = 1 AND lm.IsActive = 1
              AND NOT EXISTS (
                  SELECT 1 FROM [dbo].[ActivityAssignments] existing
                  WHERE existing.StudentProfileId = @EstId AND existing.ActivityId = a.Id AND existing.IsActive = 1
                    AND (CAST(existing.FechaInicio AS DATE) = @FechaBase OR existing.Estado IN (N'Pendiente', N'EnProgreso'))
              )
            ORDER BY CASE WHEN @TasaFallo >= 40.0 AND a.TipoActividad = @TipoDebil THEN 0 ELSE 1 END ASC, lm.Orden ASC, a.CreatedAt ASC;

            DECLARE @CandIdx INT = 1, @CandTotal INT = (SELECT COUNT(1) FROM @Candidatas);
            WHILE @CandIdx <= @CandTotal
            BEGIN
                SET @OrdenActual = @OrdenActual + 1;
                DECLARE @ActId UNIQUEIDENTIFIER, @ActTitulo NVARCHAR(200), @ActTipo NVARCHAR(50), @Prio NVARCHAR(20), @Motivo NVARCHAR(300);
                DECLARE @NewAssignId UNIQUEIDENTIFIER = NEWID();

                SELECT @ActId = ActivityId, @ActTitulo = Titulo, @ActTipo = TipoActividad, @Prio = PrioridadSugerida, @Motivo = Motivo
                FROM @Candidatas WHERE CandRow = @CandIdx;

                DECLARE @DiasPlazo INT = CASE WHEN @SinTemp = 1 OR @ModoTemp IN (N'sin-temporizador', N'tiempo-extendido') THEN 9 ELSE 7 END;
                DECLARE @FechaLim DATETIME2 = DATEADD(DAY, @DiasPlazo, CAST(@FechaBase AS DATETIME2));

                INSERT INTO [dbo].[ActivityAssignments] ([Id], [ActivityId], [StudentProfileId], [FechaInicio], [FechaLimite], [OrdenSecuencial], [Prioridad], [Estado], [CreatedAt], [IsActive])
                VALUES (@NewAssignId, @ActId, @EstId, CAST(@FechaBase AS DATETIME2), @FechaLim, @OrdenActual, @Prio, N'Pendiente', @AhoraUtc, 1);

                INSERT INTO #ResumenAsignacionesCreadas (AssignmentId, StudentProfileId, CodigoEstudiante, EstudianteNombre, ActivityId, ActividadTitulo, TipoActividad, Prioridad, OrdenSecuencial, FechaInicio, FechaLimite, MotivoAdaptativo)
                VALUES (@NewAssignId, @EstId, @CodEst, @NomEst, @ActId, @ActTitulo, @ActTipo, @Prio, @OrdenActual, CAST(@FechaBase AS DATETIME2), @FechaLim, @Motivo);

                SET @CandIdx = @CandIdx + 1;
            END;

            SET @IdxEst = @IdxEst + 1;
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    SELECT AssignmentId, StudentProfileId, CodigoEstudiante, EstudianteNombre, ActivityId, ActividadTitulo, TipoActividad, Prioridad, OrdenSecuencial, FechaInicio, FechaLimite, MotivoAdaptativo
    FROM #ResumenAsignacionesCreadas ORDER BY CodigoEstudiante ASC, OrdenSecuencial ASC;
END;
GO

-- -------------------------------------------------------------------------------------------------
-- STORED PROCEDURE 4: sp_EvaluarAlertasYRecomendacionesTEA
-- Incluye: Tablas temporales (#EstudiantesAnalizar, #HallazgosTEA) y 2 ciclos WHILE (evaluación de 4
--          heurísticas clínicas TEA por estudiante + inserción idempotente en PedagogicalNotes).
-- -------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_EvaluarAlertasYRecomendacionesTEA]
    @ClassroomId UNIQUEIDENTIFIER = NULL,
    @DiasAnalisis INT = 14,
    @RegistrarNotasAutomaticas BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF @DiasAnalisis IS NULL OR @DiasAnalisis < 3 SET @DiasAnalisis = 14;
    DECLARE @AhoraUtc DATETIME2 = SYSUTCDATETIME();
    DECLARE @FechaCorte DATETIME2 = DATEADD(DAY, -@DiasAnalisis, @AhoraUtc);

    CREATE TABLE #EstudiantesAnalizar (
        RowNum INT IDENTITY(1,1) PRIMARY KEY,
        StudentProfileId UNIQUEIDENTIFIER NOT NULL,
        CodigoEstudiante NVARCHAR(50) NOT NULL,
        EstudianteNombre NVARCHAR(220) NOT NULL,
        TeacherProfileId UNIQUEIDENTIFIER NULL,
        OcultarTemporizadores BIT NOT NULL,
        AudioActivo BIT NOT NULL,
        VolumenAudio INT NOT NULL
    );

    INSERT INTO #EstudiantesAnalizar (StudentProfileId, CodigoEstudiante, EstudianteNombre, TeacherProfileId, OcultarTemporizadores, AudioActivo, VolumenAudio)
    SELECT
        sp.Id, sp.CodigoEstudiante,
        LTRIM(RTRIM(ISNULL(u.Nombres, N'') + N' ' + ISNULL(u.Apellidos, N''))),
        COALESCE(c.TeacherProfileId, (SELECT TOP 1 tp.Id FROM [dbo].[TeacherProfiles] tp WHERE tp.IsActive = 1 ORDER BY tp.CreatedAt)),
        ISNULL(spf.OcultarTemporizadores, 1), ISNULL(spf.AudioActivo, 1), ISNULL(spf.VolumenAudio, 50)
    FROM [dbo].[StudentProfiles] sp
    INNER JOIN [dbo].[AspNetUsers] u ON u.Id = sp.UserId
    LEFT JOIN [dbo].[Classrooms] c ON c.Id = sp.ClassroomId
    LEFT JOIN [dbo].[SensoryPreferences] spf ON spf.StudentProfileId = sp.Id
    WHERE sp.IsActive = 1 AND (@ClassroomId IS NULL OR sp.ClassroomId = @ClassroomId)
    ORDER BY sp.CodigoEstudiante ASC;

    CREATE TABLE #HallazgosTEA (
        HallazgoId INT IDENTITY(1,1) PRIMARY KEY,
        StudentProfileId UNIQUEIDENTIFIER NOT NULL,
        CodigoEstudiante NVARCHAR(50) NOT NULL,
        EstudianteNombre NVARCHAR(220) NOT NULL,
        TeacherProfileId UNIQUEIDENTIFIER NULL,
        Area NVARCHAR(30) NOT NULL,
        TipoAlerta NVARCHAR(30) NOT NULL,
        Severidad NVARCHAR(20) NOT NULL,
        Titulo NVARCHAR(100) NOT NULL,
        DescripcionHallazgo NVARCHAR(2000) NOT NULL,
        RecomendacionCasa NVARCHAR(500) NOT NULL,
        AjusteSensorialSugerido NVARCHAR(300) NOT NULL,
        NotaInsertada BIT NOT NULL DEFAULT 0
    );

    DECLARE @Idx INT = 1, @Total INT = (SELECT COUNT(1) FROM #EstudiantesAnalizar);
    WHILE @Idx <= @Total
    BEGIN
        DECLARE @EstId UNIQUEIDENTIFIER, @CodEst NVARCHAR(50), @NomEst NVARCHAR(220), @TeachId UNIQUEIDENTIFIER, @OcultarTemp BIT;
        SELECT @EstId = StudentProfileId, @CodEst = CodigoEstudiante, @NomEst = EstudianteNombre, @TeachId = TeacherProfileId, @OcultarTemp = OcultarTemporizadores
        FROM #EstudiantesAnalizar WHERE RowNum = @Idx;

        DECLARE @EpisodiosFrustracion INT = 0, @TiempoPromVentana INT = 0, @TotalExecVentana INT = 0, @ExitosVentana INT = 0, @PendientesActivos INT = 0;

        SELECT
            @TotalExecVentana = COUNT(1),
            @ExitosVentana = ISNULL(SUM(CASE WHEN ae.CompletadaExitosamente = 1 THEN 1 ELSE 0 END), 0),
            @EpisodiosFrustracion = ISNULL(SUM(CASE WHEN ae.CompletadaExitosamente = 0 AND ae.CantidadIntentosFallidos >= 4 THEN 1 ELSE 0 END), 0),
            @TiempoPromVentana = ISNULL(AVG(ae.TiempoRespuestaSegundos), 0)
        FROM [dbo].[ActivityExecutions] ae
        WHERE ae.StudentProfileId = @EstId AND ae.IsActive = 1 AND ae.FechaRegistro >= @FechaCorte;

        SELECT @PendientesActivos = COUNT(1)
        FROM [dbo].[ActivityAssignments] aa
        WHERE aa.StudentProfileId = @EstId AND aa.IsActive = 1 AND aa.Estado IN (N'Pendiente', N'EnProgreso');

        IF @EpisodiosFrustracion >= 2
        BEGIN
            INSERT INTO #HallazgosTEA (StudentProfileId, CodigoEstudiante, EstudianteNombre, TeacherProfileId, Area, TipoAlerta, Severidad, Titulo, DescripcionHallazgo, RecomendacionCasa, AjusteSensorialSugerido)
            VALUES (@EstId, @CodEst, @NomEst, @TeachId, N'Cognitiva', N'Alerta', N'Alta', N'[SP-TEA] Patrón de frustración en actividades recientes',
                CONCAT(N'El estudiante registró ', CAST(@EpisodiosFrustracion AS NVARCHAR(10)), N' sesiones con 4 o más intentos fallidos no completados en los últimos ', CAST(@DiasAnalisis AS NVARCHAR(10)), N' días.'),
                N'Dividir la práctica en bloques cortos de 5 minutos con apoyos pictográficos previos.',
                N'Activar Animaciones Reducidas y priorizar actividades de Emparejamiento Visual.');
        END

        IF @TotalExecVentana > 0 AND @TiempoPromVentana >= 90
        BEGIN
            INSERT INTO #HallazgosTEA (StudentProfileId, CodigoEstudiante, EstudianteNombre, TeacherProfileId, Area, TipoAlerta, Severidad, Titulo, DescripcionHallazgo, RecomendacionCasa, AjusteSensorialSugerido)
            VALUES (@EstId, @CodEst, @NomEst, @TeachId, N'Sensorial', N'Observacion', N'Media', N'[SP-TEA] Tiempo de respuesta prolongado detectado',
                CONCAT(N'El tiempo promedio por actividad es de ', CAST(@TiempoPromVentana AS NVARCHAR(10)), N' segundos en el periodo analizado.'),
                N'Permitir que el estudiante explore cada paso a su propio ritmo sin indicaciones de prisa.',
                CASE WHEN @OcultarTemp = 0 THEN N'Activar OcultarTemporizadores = true (modo sin-temporizador).' ELSE N'Mantener temporizador oculto y ampliar escala tipográfica a nivel 3.' END);
        END

        IF @TotalExecVentana = 0 AND @PendientesActivos > 0
        BEGIN
            INSERT INTO #HallazgosTEA (StudentProfileId, CodigoEstudiante, EstudianteNombre, TeacherProfileId, Area, TipoAlerta, Severidad, Titulo, DescripcionHallazgo, RecomendacionCasa, AjusteSensorialSugerido)
            VALUES (@EstId, @CodEst, @NomEst, @TeachId, N'Conductual', N'Alerta', N'Media', N'[SP-TEA] Pausa prolongada en rutina asignada',
                CONCAT(N'El estudiante cuenta con ', CAST(@PendientesActivos AS NVARCHAR(10)), N' actividades pendientes en su agenda sin ejecuciones en los últimos ', CAST(@DiasAnalisis AS NVARCHAR(10)), N' días.'),
                N'Retomar la agenda con una única actividad diaria en horario fijo predecible.',
                N'Verificar que el inicio de sesión con apoyo del acudiente esté disponible.');
        END

        IF @TotalExecVentana >= 3 AND @ExitosVentana = @TotalExecVentana AND @EpisodiosFrustracion = 0
        BEGIN
            INSERT INTO #HallazgosTEA (StudentProfileId, CodigoEstudiante, EstudianteNombre, TeacherProfileId, Area, TipoAlerta, Severidad, Titulo, DescripcionHallazgo, RecomendacionCasa, AjusteSensorialSugerido)
            VALUES (@EstId, @CodEst, @NomEst, @TeachId, N'Socioemocional', N'Logro', N'Positiva', N'[SP-TEA] Dominio constante de rutina de aprendizaje',
                CONCAT(N'Completó exitosamente el 100% de sus ', CAST(@TotalExecVentana AS NVARCHAR(10)), N' ejecuciones recientes sin episodios de frustración.'),
                N'Felicitar el esfuerzo constante y presentar el próximo módulo de secuencia lógica.',
                N'Configuración sensorial óptima; conservar parámetros actuales.');
        END

        SET @Idx = @Idx + 1;
    END;

    IF @RegistrarNotasAutomaticas = 1
    BEGIN
        DECLARE @HIdx INT = 1, @HTotal INT = (SELECT COUNT(1) FROM #HallazgosTEA);
        WHILE @HIdx <= @HTotal
        BEGIN
            DECLARE @HStudentId UNIQUEIDENTIFIER, @HTeacherId UNIQUEIDENTIFIER, @HArea NVARCHAR(30), @HTipo NVARCHAR(30), @HTitulo NVARCHAR(100), @HDesc NVARCHAR(2000), @HRec NVARCHAR(500);
            SELECT @HStudentId = StudentProfileId, @HTeacherId = TeacherProfileId, @HArea = Area, @HTipo = TipoAlerta, @HTitulo = Titulo, @HDesc = DescripcionHallazgo, @HRec = RecomendacionCasa
            FROM #HallazgosTEA WHERE HallazgoId = @HIdx;

            IF @HTeacherId IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM [dbo].[PedagogicalNotes] pn
                WHERE pn.StudentProfileId = @HStudentId AND pn.Titulo = @HTitulo AND pn.FechaRegistro >= DATEADD(DAY, -7, @AhoraUtc)
            )
            BEGIN
                INSERT INTO [dbo].[PedagogicalNotes] ([Id], [StudentProfileId], [TeacherProfileId], [AssignmentId], [FechaRegistro], [Area], [Tipo], [Titulo], [Contenido], [RecomendacionCasa], [VisibleAcudiente], [CreatedAt], [IsActive])
                VALUES (NEWID(), @HStudentId, @HTeacherId, NULL, @AhoraUtc, @HArea, @HTipo, @HTitulo, @HDesc, @HRec, 1, @AhoraUtc, 1);

                UPDATE #HallazgosTEA SET NotaInsertada = 1 WHERE HallazgoId = @HIdx;
            END

            SET @HIdx = @HIdx + 1;
        END;
    END

    SELECT HallazgoId, StudentProfileId, CodigoEstudiante, EstudianteNombre, Area, TipoAlerta, Severidad, Titulo, DescripcionHallazgo, RecomendacionCasa, AjusteSensorialSugerido, NotaInsertada
    FROM #HallazgosTEA
    ORDER BY CASE Severidad WHEN N'Alta' THEN 1 WHEN N'Media' THEN 2 ELSE 3 END, CodigoEstudiante ASC;
END;
GO

-- -------------------------------------------------------------------------------------------------
-- STORED PROCEDURE 5: sp_RecalcularProgresoYLogrosEstudiante
-- Incluye: Transacción explícita con TRY/CATCH, tablas temporales (#ModulosAudit, #InsigniasEvaluadas)
--          y 2 ciclos WHILE (auditoría de módulos con hitos + evaluación de 7 insignias no competitivas).
-- -------------------------------------------------------------------------------------------------
CREATE OR ALTER PROCEDURE [dbo].[sp_RecalcularProgresoYLogrosEstudiante]
    @StudentProfileId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AhoraUtc DATETIME2 = SYSUTCDATETIME();
    DECLARE @AsignacionesReconciliadas INT = 0;
    DECLARE @NombreEstudiante NVARCHAR(100) = N'Estudiante';
    DECLARE @TeacherProfileId UNIQUEIDENTIFIER = NULL;

    SELECT
        @NombreEstudiante = ISNULL(u.Nombres, N'Estudiante'),
        @TeacherProfileId = COALESCE(c.TeacherProfileId, (SELECT TOP 1 tp.Id FROM [dbo].[TeacherProfiles] tp WHERE tp.IsActive = 1 ORDER BY tp.CreatedAt))
    FROM [dbo].[StudentProfiles] sp
    INNER JOIN [dbo].[AspNetUsers] u ON u.Id = sp.UserId
    LEFT JOIN [dbo].[Classrooms] c ON c.Id = sp.ClassroomId
    WHERE sp.Id = @StudentProfileId;

    CREATE TABLE #ModulosAudit (
        RowNum INT IDENTITY(1,1) PRIMARY KEY,
        ModuloId UNIQUEIDENTIFIER NOT NULL,
        Titulo NVARCHAR(150) NOT NULL,
        Categoria NVARCHAR(150) NOT NULL,
        Orden INT NOT NULL,
        ActividadesTotales INT NOT NULL DEFAULT 0,
        ActividadesCompletadas INT NOT NULL DEFAULT 0,
        PorcentajeAvance INT NOT NULL DEFAULT 0,
        Completado BIT NOT NULL DEFAULT 0,
        FechaCompletado DATETIME2 NULL
    );

    CREATE TABLE #InsigniasEvaluadas (
        BadgeIdx INT NOT NULL PRIMARY KEY,
        Clave NVARCHAR(50) NOT NULL,
        Titulo NVARCHAR(100) NOT NULL,
        Descripcion NVARCHAR(300) NOT NULL,
        Icono NVARCHAR(50) NOT NULL,
        Desbloqueado BIT NOT NULL DEFAULT 0,
        FechaDesbloqueo DATETIME2 NULL
    );

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE aa
        SET aa.Estado = N'Completada', aa.UpdatedAt = @AhoraUtc
        FROM [dbo].[ActivityAssignments] aa
        WHERE aa.StudentProfileId = @StudentProfileId AND aa.IsActive = 1 AND aa.Estado <> N'Completada'
          AND EXISTS (SELECT 1 FROM [dbo].[ActivityExecutions] ae WHERE ae.AssignmentId = aa.Id AND ae.IsActive = 1 AND ae.CompletadaExitosamente = 1);

        SET @AsignacionesReconciliadas = @AsignacionesReconciliadas + @@ROWCOUNT;

        UPDATE aa
        SET aa.Estado = N'EnProgreso', aa.UpdatedAt = @AhoraUtc
        FROM [dbo].[ActivityAssignments] aa
        WHERE aa.StudentProfileId = @StudentProfileId AND aa.IsActive = 1 AND aa.Estado = N'Pendiente'
          AND EXISTS (SELECT 1 FROM [dbo].[ActivityExecutions] ae WHERE ae.AssignmentId = aa.Id AND ae.IsActive = 1);

        SET @AsignacionesReconciliadas = @AsignacionesReconciliadas + @@ROWCOUNT;

        INSERT INTO #ModulosAudit (ModuloId, Titulo, Categoria, Orden)
        SELECT lm.Id, lm.Titulo, ISNULL(lc.Nombre, N'General'), lm.Orden
        FROM [dbo].[LearningModules] lm
        LEFT JOIN [dbo].[LearningCategories] lc ON lc.Id = lm.CategoriaId
        WHERE lm.IsActive = 1 ORDER BY lm.Orden ASC;

        DECLARE @ModIdx INT = 1, @ModTotal INT = (SELECT COUNT(1) FROM #ModulosAudit);
        WHILE @ModIdx <= @ModTotal
        BEGIN
            DECLARE @ModId UNIQUEIDENTIFIER, @ModTitulo NVARCHAR(150);
            SELECT @ModId = ModuloId, @ModTitulo = Titulo FROM #ModulosAudit WHERE RowNum = @ModIdx;

            DECLARE @ActTot INT = (SELECT COUNT(1) FROM [dbo].[Activities] a WHERE a.ModuloId = @ModId AND a.IsActive = 1);
            DECLARE @ActComp INT = 0, @FechaUltExito DATETIME2 = NULL;

            SELECT @ActComp = COUNT(DISTINCT a.Id), @FechaUltExito = MAX(ae.FechaRegistro)
            FROM [dbo].[Activities] a
            INNER JOIN [dbo].[ActivityAssignments] aa ON aa.ActivityId = a.Id AND aa.StudentProfileId = @StudentProfileId AND aa.IsActive = 1
            LEFT JOIN [dbo].[ActivityExecutions] ae ON ae.AssignmentId = aa.Id AND ae.StudentProfileId = @StudentProfileId AND ae.IsActive = 1 AND ae.CompletadaExitosamente = 1
            WHERE a.ModuloId = @ModId AND a.IsActive = 1 AND (aa.Estado = N'Completada' OR ae.Id IS NOT NULL);

            DECLARE @Pct INT = CASE WHEN @ActTot = 0 THEN 0 ELSE CAST(ROUND((CAST(@ActComp AS DECIMAL(10,2)) * 100.0) / @ActTot, 0) AS INT) END;
            DECLARE @IsComp BIT = CASE WHEN @ActTot > 0 AND @ActComp >= @ActTot THEN 1 ELSE 0 END;

            UPDATE #ModulosAudit
            SET ActividadesTotales = @ActTot, ActividadesCompletadas = @ActComp, PorcentajeAvance = @Pct,
                Completado = @IsComp, FechaCompletado = CASE WHEN @IsComp = 1 THEN ISNULL(@FechaUltExito, @AhoraUtc) ELSE NULL END
            WHERE RowNum = @ModIdx;

            IF @IsComp = 1 AND @TeacherProfileId IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM [dbo].[PedagogicalNotes] pn
                WHERE pn.StudentProfileId = @StudentProfileId AND pn.Tipo = N'Logro' AND pn.Titulo = LEFT(CONCAT(N'[HITO-MÓDULO] ', @ModTitulo), 100)
            )
            BEGIN
                INSERT INTO [dbo].[PedagogicalNotes] ([Id], [StudentProfileId], [TeacherProfileId], [AssignmentId], [FechaRegistro], [Area], [Tipo], [Titulo], [Contenido], [RecomendacionCasa], [VisibleAcudiente], [CreatedAt], [IsActive])
                VALUES (NEWID(), @StudentProfileId, @TeacherProfileId, NULL, ISNULL(@FechaUltExito, @AhoraUtc), N'Socioemocional', N'Logro',
                    LEFT(CONCAT(N'[HITO-MÓDULO] ', @ModTitulo), 100),
                    CONCAT(N'El estudiante completó satisfactoriamente todas las actividades del módulo "', @ModTitulo, '".'),
                    N'Celebrar en familia este logro de constancia y autonomía.', 1, @AhoraUtc, 1);
            END

            SET @ModIdx = @ModIdx + 1;
        END;

        INSERT INTO #InsigniasEvaluadas (BadgeIdx, Clave, Titulo, Descripcion, Icono)
        VALUES
            (1, N'primer_paso', N'Primer Paso', N'Completaste tu primera actividad de aprendizaje en la plataforma.', N'award'),
            (2, N'explorador_visual', N'Explorador Visual', N'Completaste con éxito una actividad de emparejamiento visual.', N'eye'),
            (3, N'maestro_secuencias', N'Maestro de Secuencias', N'Organizaste los pasos en una actividad de secuencia lógica.', N'list-ordered'),
            (4, N'campeon_calma', N'Campeón de la Calma', N'Exploraste y reconociste emociones en actividades de asociación emocional.', N'heart'),
            (5, N'constructor_vinculos', N'Constructor de Vínculos', N'Completaste una actividad de interacción y rutina social.', N'users'),
            (6, N'habito_constante', N'Hábito Constante', N'Superaste 5 actividades demostrando gran constancia y dedicación.', N'check-circle'),
            (7, N'super_perseverante', N'Super Perseverante', N'Alcanzaste 10 actividades completadas a tu propio ritmo.', N'sparkles');

        DECLARE @TotalExitos INT = (SELECT COUNT(1) FROM [dbo].[ActivityExecutions] ae WHERE ae.StudentProfileId = @StudentProfileId AND ae.IsActive = 1 AND ae.CompletadaExitosamente = 1);
        DECLARE @BIdx INT = 1;
        WHILE @BIdx <= 7
        BEGIN
            DECLARE @Desbloq BIT = 0, @FechaDesbloq DATETIME2 = NULL;

            IF @BIdx = 1
            BEGIN
                SELECT TOP 1 @Desbloq = 1, @FechaDesbloq = ae.FechaRegistro
                FROM [dbo].[ActivityExecutions] ae WHERE ae.StudentProfileId = @StudentProfileId AND ae.IsActive = 1 AND ae.CompletadaExitosamente = 1 ORDER BY ae.FechaRegistro ASC;
            END
            ELSE IF @BIdx IN (2, 3, 4, 5)
            BEGIN
                DECLARE @TipoBuscado NVARCHAR(50) = CASE @BIdx WHEN 2 THEN N'EmparejamientoVisual' WHEN 3 THEN N'SecuenciaLogica' WHEN 4 THEN N'AsociacionEmocional' WHEN 5 THEN N'RutinaSocial' END;
                SELECT TOP 1 @Desbloq = 1, @FechaDesbloq = ae.FechaRegistro
                FROM [dbo].[ActivityExecutions] ae
                INNER JOIN [dbo].[ActivityAssignments] aa ON aa.Id = ae.AssignmentId
                INNER JOIN [dbo].[Activities] a ON a.Id = aa.ActivityId
                WHERE ae.StudentProfileId = @StudentProfileId AND ae.IsActive = 1 AND ae.CompletadaExitosamente = 1 AND a.TipoActividad = @TipoBuscado
                ORDER BY ae.FechaRegistro ASC;
            END
            ELSE IF @BIdx = 6 AND @TotalExitos >= 5
            BEGIN
                SET @Desbloq = 1;
                SELECT @FechaDesbloq = FechaRegistro FROM (SELECT ae.FechaRegistro, ROW_NUMBER() OVER (ORDER BY ae.FechaRegistro ASC) AS Rn FROM [dbo].[ActivityExecutions] ae WHERE ae.StudentProfileId = @StudentProfileId AND ae.IsActive = 1 AND ae.CompletadaExitosamente = 1) t WHERE t.Rn = 5;
            END
            ELSE IF @BIdx = 7 AND @TotalExitos >= 10
            BEGIN
                SET @Desbloq = 1;
                SELECT @FechaDesbloq = FechaRegistro FROM (SELECT ae.FechaRegistro, ROW_NUMBER() OVER (ORDER BY ae.FechaRegistro ASC) AS Rn FROM [dbo].[ActivityExecutions] ae WHERE ae.StudentProfileId = @StudentProfileId AND ae.IsActive = 1 AND ae.CompletadaExitosamente = 1) t WHERE t.Rn = 10;
            END

            UPDATE #InsigniasEvaluadas SET Desbloqueado = @Desbloq, FechaDesbloqueo = @FechaDesbloq WHERE BadgeIdx = @BIdx;
            SET @BIdx = @BIdx + 1;
        END;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;

    DECLARE @MensajeMotivacional NVARCHAR(300) = CASE
        WHEN @TotalExitos = 0 THEN CONCAT(N'¡Hola ', @NombreEstudiante, N'! Tienes grandes aventuras de aprendizaje esperando por ti.')
        WHEN @TotalExitos < 5 THEN CONCAT(N'¡Buen trabajo ', @NombreEstudiante, N'! Cada paso que das te ayuda a descubrir cosas nuevas.')
        WHEN @TotalExitos < 10 THEN CONCAT(N'¡Increíble dedicación ', @NombreEstudiante, N'! Estás construyendo grandes habilidades a tu propio ritmo.')
        ELSE CONCAT(N'¡Orgullo total ', @NombreEstudiante, N'! Tu esfuerzo y constancia son admirables.')
    END;

    SELECT
        @StudentProfileId AS StudentProfileId,
        @AsignacionesReconciliadas AS AsignacionesReconciliadas,
        @TotalExitos AS TotalActividadesCompletadas,
        (SELECT COUNT(1) FROM #ModulosAudit WHERE Completado = 1) AS ModulosCompletados,
        (SELECT COUNT(1) FROM #InsigniasEvaluadas WHERE Desbloqueado = 1) AS InsigniasDesbloqueadas,
        @MensajeMotivacional AS MensajeMotivacional,
        @AhoraUtc AS FechaRecalculoUtc;

    SELECT Clave, Titulo, Descripcion, Icono, Desbloqueado, FechaDesbloqueo FROM #InsigniasEvaluadas ORDER BY BadgeIdx ASC;
    SELECT ModuloId, Titulo, Categoria, Orden, ActividadesTotales, ActividadesCompletadas, PorcentajeAvance, Completado, FechaCompletado FROM #ModulosAudit ORDER BY Orden ASC;
END;
GO
