$baseUrl = "http://localhost:5264"
$ErrorActionPreference = "Stop"

function Post-Json($url, $body, $token = $null) {
    $headers = @{ "Content-Type" = "application/json" }
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    return Invoke-RestMethod -Uri $url -Method Post -Body ($body | ConvertTo-Json -Depth 10) -Headers $headers
}

function Get-Json($url, $token = $null) {
    $headers = @{ "Content-Type" = "application/json" }
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    return Invoke-RestMethod -Uri $url -Method Get -Headers $headers
}

Write-Host "=== 1. Autenticacion de Usuarios de Prueba ===" -ForegroundColor Cyan
$docenteLogin = Post-Json "$baseUrl/api/auth/login" @{ email = "docente@tealearning.com"; password = "Docente123!*" }
$docenteToken = $docenteLogin.data.token
Write-Host "Token Docente obtenido con exito." -ForegroundColor Green

$acudienteLogin = Post-Json "$baseUrl/api/auth/login" @{ email = "acudiente@tealearning.com"; password = "Acudiente123!*" }
$acudienteToken = $acudienteLogin.data.token
Write-Host "Token Acudiente obtenido con exito." -ForegroundColor Green

$estudianteLogin = Post-Json "$baseUrl/api/auth/login" @{ email = "estudiante@tealearning.com"; password = "Estudiante123!*" }
$estudianteToken = $estudianteLogin.data.token
Write-Host "Token Estudiante obtenido con exito." -ForegroundColor Green

Write-Host "`n=== 2. Obtener Perfil del Estudiante Mateo ===" -ForegroundColor Cyan
$studentProfile = Get-Json "$baseUrl/api/students/me/profile" $estudianteToken
$studentId = $studentProfile.data.id
$classroomId = $studentProfile.data.classroomId
Write-Host "Estudiante ID: $studentId | Classroom ID: $classroomId" -ForegroundColor Yellow

Write-Host "`n=== 3. HU-008: Panel Analitico del Docente ===" -ForegroundColor Cyan
$studentAnalytics = Get-Json "$baseUrl/api/analytics/teacher/students/$studentId/summary" $docenteToken
Write-Host "Resumen del Estudiante:" -ForegroundColor Green
Write-Host "  Nombre: $($studentAnalytics.data.nombreCompleto)"
Write-Host "  Tasa Aciertos Global: $($studentAnalytics.data.tasaAciertosGlobal)%"
Write-Host "  Promedio Tiempo Respuesta: $($studentAnalytics.data.promedioTiempoRespuestaSegundos)s"
Write-Host "  Actividades Completadas: $($studentAnalytics.data.totalActividadesCompletadas)"
Write-Host "  Actividades Pendientes: $($studentAnalytics.data.totalActividadesPendientes)"

if ($classroomId) {
    $classAnalytics = Get-Json "$baseUrl/api/analytics/teacher/classes/$classroomId" $docenteToken
    Write-Host "Resumen del Aula ($($classAnalytics.data.classroomNombre)):" -ForegroundColor Green
    Write-Host "  Tasa Aciertos Promedio: $($classAnalytics.data.tasaAciertosPromedioAula)%"
    Write-Host "  Total Estudiantes en Aula: $($classAnalytics.data.totalEstudiantes)"
}

Write-Host "`n=== 4. HU-010: Bitacora Pedagogica (Notas Cualitativas) ===" -ForegroundColor Cyan
$newNote = Post-Json "$baseUrl/api/students/$studentId/pedagogical-notes" @{
    contenido = "Mateo respondio con calma y concentracion al estimulo visual pastel durante la jornada de hoy. Se evidencia mejor autorregulacion."
} $docenteToken
Write-Host "Nota cualitativa creada con exito. ID: $($newNote.data.id)" -ForegroundColor Green

$notesDocente = Get-Json "$baseUrl/api/students/$studentId/pedagogical-notes" $docenteToken
Write-Host "Docente consulto notas ($($notesDocente.data.Count) notas encontradas)." -ForegroundColor Green

$notesAcudiente = Get-Json "$baseUrl/api/students/$studentId/pedagogical-notes" $acudienteToken
Write-Host "Acudiente consulto notas con exito ($($notesAcudiente.data.Count) notas accesibles para la familia)." -ForegroundColor Green

Write-Host "`n=== 5. HU-009: Monitoreo del Acudiente ===" -ForegroundColor Cyan
$guardianProgress = Get-Json "$baseUrl/api/guardian/students/$studentId/progress" $acudienteToken
Write-Host "Progreso segun Acudiente:" -ForegroundColor Green
Write-Host "  Estudiante: $($guardianProgress.data.studentNombre)"
Write-Host "  Avance: $($guardianProgress.data.porcentajeAvance)%"
Write-Host "  Tiempo Invertido: $($guardianProgress.data.tiempoTotalInvertidoMinutos) min"
Write-Host "  Mensaje Pedagogico: $($guardianProgress.data.mensajePedagogico)"

Write-Host "`n=== 6. HU-011: Logros e Insignias del Estudiante ===" -ForegroundColor Cyan
$achievements = Get-Json "$baseUrl/api/students/me/achievements" $estudianteToken
Write-Host "Logros del Estudiante:" -ForegroundColor Green
Write-Host "  Total Completadas: $($achievements.data.totalActividadesCompletadas)"
Write-Host "  Total Modulos Concluidos: $($achievements.data.totalModulosConcluidos)"
Write-Host "  Mensaje Motivacional: $($achievements.data.mensajeMotivacional)"
Write-Host "  Insignias Totales: $($achievements.data.insignias.Count)"
foreach ($badge in $achievements.data.insignias) {
    Write-Host "   - [$($badge.clave)] $($badge.titulo): $(if ($badge.desbloqueado) { 'DESBLOQUEADO' } else { 'BLOQUEADO' })"
}

Write-Host "`n=== 7. Simulacion de Ejecucion Pedagogica (Estudiante completa actividad) ===" -ForegroundColor Cyan
$routine = Get-Json "$baseUrl/api/students/me/daily-routine" $estudianteToken
if ($routine.data.Count -gt 0) {
    $firstItem = $routine.data[0]
    $assignmentId = $firstItem.assignmentId
    Write-Host "Iniciando actividad: $($firstItem.titulo) (Assignment: $assignmentId)..." -ForegroundColor Yellow
    
    $startResult = Post-Json "$baseUrl/api/activities/$assignmentId/start" @{} $estudianteToken
    Write-Host "Actividad iniciada con configuracion sensorial adaptada." -ForegroundColor Green
    
    $submitBody = @{
        tiempoRespuestaSegundos = 42
        cantidadAciertos = 5
        cantidadIntentosFallidos = 1
        completadaExitosamente = $true
    }
    $submitResult = Post-Json "$baseUrl/api/activities/$assignmentId/submit" $submitBody $estudianteToken
    Write-Host "Actividad enviada con exito: $($submitResult.data.retroalimentacion)" -ForegroundColor Green

    Write-Host "`n=== 8. Verificacion de Actualizacion Post-Ejecucion ===" -ForegroundColor Cyan
    # Re-consultar logros (HU-011)
    $updatedAchievements = Get-Json "$baseUrl/api/students/me/achievements" $estudianteToken
    Write-Host "Logros Actualizados:" -ForegroundColor Green
    Write-Host "  Total Completadas: $($updatedAchievements.data.totalActividadesCompletadas)"
    foreach ($badge in $updatedAchievements.data.insignias) {
        Write-Host "   - [$($badge.clave)] $($badge.titulo): $(if ($badge.desbloqueado) { 'DESBLOQUEADO' } else { 'BLOQUEADO' })"
    }

    # Re-consultar progreso acudiente (HU-009)
    $updatedGuardian = Get-Json "$baseUrl/api/guardian/students/$studentId/progress" $acudienteToken
    Write-Host "Progreso Acudiente Actualizado:" -ForegroundColor Green
    Write-Host "  Avance: $($updatedGuardian.data.porcentajeAvance)%"
    Write-Host "  Tiempo invertido: $($updatedGuardian.data.tiempoTotalInvertidoMinutos) min"
    Write-Host "  Ultimas actividades registradas: $($updatedGuardian.data.ultimasActividades.Count)"

    # Re-consultar analitica docente (HU-008)
    $updatedTeacher = Get-Json "$baseUrl/api/analytics/teacher/students/$studentId/summary" $docenteToken
    Write-Host "Analitica Docente Actualizada:" -ForegroundColor Green
    Write-Host "  Tasa de Aciertos Global: $($updatedTeacher.data.tasaAciertosGlobal)%"
    Write-Host "  Promedio Tiempo: $($updatedTeacher.data.promedioTiempoRespuestaSegundos)s"
    Write-Host "  Curva Intentos Puntos: $($updatedTeacher.data.curvaIntentos.Count)"
    Write-Host "  Evolucion Semanal Puntos: $($updatedTeacher.data.evolucionSemanal.Count)"
}

Write-Host "`n[OK] SPRINT 3 VERIFICACION COMPLETADA EXITOSAMENTE [OK]" -ForegroundColor Green
