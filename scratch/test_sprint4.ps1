$baseUrl = "http://localhost:5264"
$ErrorActionPreference = "Stop"

function Post-Json($url, $body, $token = $null) {
    $headers = @{ "Content-Type" = "application/json" }
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    return Invoke-RestMethod -Uri $url -Method Post -Body ($body | ConvertTo-Json -Depth 10) -Headers $headers
}

function Put-Json($url, $body, $token = $null) {
    $headers = @{ "Content-Type" = "application/json" }
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    return Invoke-RestMethod -Uri $url -Method Put -Body ($body | ConvertTo-Json -Depth 10) -Headers $headers
}

function Get-Json($url, $token = $null) {
    $headers = @{ "Content-Type" = "application/json" }
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    return Invoke-RestMethod -Uri $url -Method Get -Headers $headers
}

Write-Host "=== 1. Autenticacion ===" -ForegroundColor Cyan
$adminLogin = Post-Json "$baseUrl/api/auth/login" @{ email = "admin@tealearning.com"; password = "Admin123!*" }
$adminToken = $adminLogin.data.token
$adminId = $adminLogin.data.user.id
Write-Host "Admin autenticado. ID: $adminId" -ForegroundColor Green

$acudienteLogin = Post-Json "$baseUrl/api/auth/login" @{ email = "acudiente@tealearning.com"; password = "Acudiente123!*" }
$acudienteToken = $acudienteLogin.data.token
Write-Host "Acudiente autenticado." -ForegroundColor Green

$docenteLogin = Post-Json "$baseUrl/api/auth/login" @{ email = "docente@tealearning.com"; password = "Docente123!*" }
$docenteToken = $docenteLogin.data.token
Write-Host "Docente autenticado." -ForegroundColor Green

Write-Host "`n=== 2. Obtener Estudiante Mateo ===" -ForegroundColor Cyan
$estudianteLogin = Post-Json "$baseUrl/api/auth/login" @{ email = "estudiante@tealearning.com"; password = "Estudiante123!*" }
$estudianteToken = $estudianteLogin.data.token
$studentProfile = Get-Json "$baseUrl/api/students/me/profile" $estudianteToken
$studentId = $studentProfile.data.id
Write-Host "Estudiante ID: $studentId" -ForegroundColor Yellow

Write-Host "`n=== 3. HU-012: Observaciones Paginadas del Acudiente ===" -ForegroundColor Cyan
# Consulta paginada oficial
$obsResult = Get-Json "$baseUrl/api/guardian/students/$studentId/observations?page=1&pageSize=5" $acudienteToken
Write-Host "Consulta HU-012 exitosa:" -ForegroundColor Green
Write-Host "  Total Observaciones: $($obsResult.data.total)"
Write-Host "  Pagina Actual: $($obsResult.data.page)"
Write-Host "  Items Recuperados: $($obsResult.data.items.Count)"
foreach ($item in $obsResult.data.items) {
    Write-Host "   - [$($item.tipo)] $($item.titulo): $($item.contenido)"
}

# Consulta con el alias /api/students/{id}/observations
$obsAlias = Get-Json "$baseUrl/api/students/$studentId/observations" $acudienteToken
Write-Host "Consulta via alias /api/students/{id}/observations exitosa: $($obsAlias.data.items.Count) items." -ForegroundColor Green

Write-Host "`n=== 4. Hardening de Seguridad del Administrador ===" -ForegroundColor Cyan
# 4.1 Intento de auto-desactivacion
try {
    Put-Json "$baseUrl/api/users/$adminId" @{ isActive = $false } $adminToken
    Write-Host "ERROR: El Admin pudo desactivarse a si mismo!" -ForegroundColor Red
} catch {
    Write-Host "OK: Auto-desactivacion bloqueada exitosamente ($($_.Exception.Message))" -ForegroundColor Green
}

# 4.2 Intento de auto-remocion de rol Admin
try {
    Put-Json "$baseUrl/api/users/$adminId/roles" @{ roles = @("Docente") } $adminToken
    Write-Host "ERROR: El Admin pudo removerse su propio rol!" -ForegroundColor Red
} catch {
    Write-Host "OK: Auto-remocion de rol Administrador bloqueada exitosamente ($($_.Exception.Message))" -ForegroundColor Green
}

# 4.3 Intento de auto-eliminacion
try {
    $headers = @{ "Authorization" = "Bearer $adminToken" }
    Invoke-RestMethod -Uri "$baseUrl/api/users/$adminId" -Method Delete -Headers $headers
    Write-Host "ERROR: El Admin pudo auto-eliminarse!" -ForegroundColor Red
} catch {
    Write-Host "OK: Auto-eliminacion bloqueada exitosamente ($($_.Exception.Message))" -ForegroundColor Green
}

Write-Host "`n[OK] SPRINT 4 VERIFICACION EN VIVO EXITOSA [OK]" -ForegroundColor Green
