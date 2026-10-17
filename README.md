# TeaLearning API - Backend & Base de Datos (TEA Nivel 1)

Backend RESTful y persistencia en SQL Server para una plataforma de apoyo pedagógico orientada a estudiantes diagnosticados con **Autismo Nivel 1 (Trastorno del Espectro Autista - TEA)**.

El sistema implementa interfaces accesibles con perfiles sensoriales personalizables, estructuración de rutinas predecibles, analítica cognitiva agregada, seguimiento pedagógico para docentes, acudientes y estudiantes, hardening de seguridad y optimización para alta disponibilidad.

---

## 🚀 Estado del Proyecto: Sprints 1, 2, 3 y 4 Finalizados (100% Completado)

| Requerimiento | Puntos | Descripción | Estado |
|---|---|---|:---:|
| **HU-001 - Gestión de Usuarios y Roles** | 5 pts | Autenticación JWT, registro, login, refresh tokens y RBAC para roles `Administrador`, `Docente`, `Estudiante`, `Acudiente`. | ✅ Completado |
| **HU-002 - Rutina Visual de Actividades** | 5 pts | Endpoint `GET /api/students/me/daily-routine` con asignaciones del día ordenadas estrictamente por su secuencia pedagógica. | ✅ Completado |
| **HU-003 - Configuración Sensorial Adaptativa** | 5 pts | Configuración de paletas cromáticas, tipografía (ej. OpenDyslexic), supresión de animaciones, escala de fuentes, contraste y temporizadores. | ✅ Completado |
| **HU-004 - Parametrización de Actividades TEA** | 8 pts | Creación y parametrización de actividades con modelo híbrido (metadata relacional + estructura dinámica `ConfigJson`). | ✅ Completado |
| **HU-005 - Asignación Individual y por Aula** | 3 pts | Asignación de actividades a un estudiante específico o en lote a un aula completa con fechas, prioridad y orden secuencial. | ✅ Completado |
| **HU-006 - Ejecución Interactiva y Telemetría** | 8 pts | Flujo de inicio (`/start`) con entrega de adaptaciones sensoriales y registro de telemetría de desempeño (`/submit`). | ✅ Completado |
| **HU-007 - Módulos Temáticos de Aprendizaje** | 3 pts | CRUD administrativo de categorías y módulos pedagógicos (`GET`, `POST`, `PUT`, `DELETE /api/modules`). | ✅ Completado |
| **HU-008 - Panel Analítico del Docente** | 5 pts | Analítica cognitiva agregada: tasa de aciertos global, tiempos por tipo de actividad, curva de intentos y evolución semanal por estudiante y aula. | ✅ Completado |
| **HU-009 - Monitoreo del Acudiente** | 5 pts | Consolidado de avance pedagógico para familias: actividades completadas vs pendientes, tiempo invertido y mensaje empático. | ✅ Completado |
| **HU-010 - Bitácora Pedagógica Docente** | 3 pts | Registro cualitativo de observaciones docentes (`POST`, `GET /api/students/{id}/pedagogical-notes`) accesible para docentes y acudientes. | ✅ Completado |
| **HU-011 - Logros e Insignias del Estudiante** | 3 pts | Gamificación no competitiva y positiva adaptada a TEA: insignias personales calculadas dinámicamente y módulos completados (`/api/students/me/achievements`). | ✅ Completado |
| **HU-012 - Observaciones Paginadas para Familias** | 2 pts | Consulta cronológica de observaciones docentes con soporte de paginación (`GET /api/guardian/students/{id}/observations`) y filtro de visibilidad. | ✅ Completado |
| **Hardening de Seguridad & Admin Protection** | — | Prevención de auto-eliminación, auto-desactivación y auto-democión del rol `Administrador`. | ✅ Completado |
| **Optimización de Base de Datos** | — | Índices compuestos en `ActivityAssignments`, `ActivityExecutions` y `PedagogicalNotes` en SQL Server. | ✅ Completado |
| **Manejo Global RFC 7807 (ProblemDetails)** | — | Middleware de excepciones con encabezado `application/problem+json` y propiedades estándar RFC 7807. | ✅ Completado |
| **Recuperación de Contraseña (SMTP + Acudiente)** | — | Restablecimiento por correo (`MailKit` SMTP + Gmail App Password) para `Administrador`, `Docente` y `Acudiente`, y restablecimiento directo por plataforma para `Estudiante` a través de su acudiente titular. | ✅ Completado |

---

## 🛠️ Stack Tecnológico

* **Plataforma & Runtime:** .NET 9 (C# 13)
* **Framework Web:** ASP.NET Core Web API
* **Persistencia & ORM:** Entity Framework Core 9 (Code First)
* **Base de Datos:** Microsoft SQL Server (LocalDB / SQL Server Express / Developer)
* **Seguridad & Auth:** ASP.NET Core Identity (`IdentityDbContext`) con hashing de contraseñas mediante **BCrypt** (`BCrypt.Net-Next`, factor de trabajo 11) en reemplazo del algoritmo predeterminado PBKDF2/SHA-256, autenticación basada en tokens JWT (`JwtBearer`), Claims estructurados, tokens de recuperación firmados (`DataProtectorTokenProvider`, 30 min) y `RateLimiter` por IP.
* **Notificaciones por Correo:** `MailKit` (SMTP sobre TLS/STARTTLS en puerto 587).
* **Documentación Interactiva:** Swagger / OpenAPI (`Swashbuckle.AspNetCore`) con soporte para autorización Bearer Token.

---

## 🏛️ Arquitectura de la Solución (Clean Layered Architecture)

```text
TeaLearningSystem/
├── TeaLearning.Api/           # Capa de Entrada / Controladores RESTful
│   ├── Controllers/           # Auth, Users, Students, SensoryPreferences, Modules, Activities, Assignments, Analytics, Guardian, PedagogicalNotes
│   ├── Middlewares/           # ExceptionHandlingMiddleware (RFC 7807 ProblemDetails / ApiResponse unificado)
│   ├── Extensions/            # Inyección de dependencias (DI), Swagger, JWT, Identity y CORS
│   └── Program.cs             # Pipeline HTTP y punto de entrada de la aplicación
│
├── TeaLearning.Services/      # Capa de Lógica de Negocio
│   ├── Common/                # ApiResponse<T> unificado
│   ├── DTOs/                  # DTOs de Auth, Users, Students, Sensory, Modules, Activities, Assignments, Execution, Analytics, Guardian, Pedagogical, Achievements
│   ├── Interfaces/            # IAuthService, IUserService, IModuleService, IActivityService, IAssignmentService, ITeacherAnalyticsService, IPedagogicalNoteService, etc.
│   ├── Security/              # BcryptPasswordHasher (IPasswordHasher<ApplicationUser> con BCrypt.Net)
│   └── Services/              # Implementaciones de lógica pedagógica, validación JSON, telemetría y analítica cognitiva
│
├── TeaLearning.Data/          # Capa de Persistencia y Acceso a Datos
│   ├── Context/               # ApplicationDbContext con Identity y DbSets de dominio
│   ├── Configurations/        # Mapeos Fluent API con índices compuestos y relaciones sin ciclos de cascada
│   ├── Repositorios/          # Repositorios genéricos y especializados (Student, Activity, Execution, PedagogicalNote, etc.)
│   ├── Migrations/            # Historial de migraciones de EF Core
│   └── Seeders/               # DatabaseSeeder para inicialización automática
│
├── TeaLearning.Core/          # Capa de Dominio (Modelos Puros)
│   ├── Common/                # BaseEntity (Id, CreatedAt, UpdatedAt, IsActive)
│   ├── Entities/              # Entidades del dominio completo
│   └── Enums/                 # UserRole, ModoContraste, TipoActividad, EstadoAsignacion, PrioridadActividad
│
└── TeaLearning.Tests/         # Pruebas Unitarias e Integración
    ├── BcryptPasswordHasherTests.cs
    ├── IdentityBcryptIntegrationTests.cs
    └── ExceptionHandlingMiddlewareTests.cs
```

---

## 📋 Estructura de Respuestas de la API

### Respuestas Exitosas y Operacionales
```json
{
  "success": true,
  "message": "Operación ejecutada con éxito",
  "data": { ... },
  "errors": []
}
```

### Respuestas de Error No Controlado (RFC 7807 ProblemDetails)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "An error occurred while processing your request.",
  "status": 500,
  "detail": "Ha ocurrido un error inesperado al procesar la solicitud.",
  "instance": "/api/...",
  "traceId": "0HN123...",
  "success": false,
  "message": "Ha ocurrido un error inesperado al procesar la solicitud.",
  "errors": [ ... ]
}
```

---

## ⚙️ Configuración y Ejecución Local

### Prerrequisitos
* [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
* SQL Server con LocalDB instalado (`(localdb)\mssqllocaldb`)
* Herramienta CLI de Entity Framework Core:
  ```bash
  dotnet tool install --global dotnet-ef
  ```

### 1. Clonar y Restaurar Dependencias
```bash
git clone https://github.com/Demax-23/PWA1-BACKEND-API.git
cd PWA1-BACKEND-API
dotnet restore
```

### 2. Configurar Secreto SMTP (Contraseña de Aplicación de Gmail)
La cuenta emisora `tealearning.notificaciones@gmail.com` está configurada en `appsettings.json`, pero la contraseña de aplicación de 16 caracteres **no se almacena en el repositorio**. Configúrala localmente con `.NET User Secrets`:
```bash
dotnet user-secrets set "Email:Password" "TU_CLAVE_DE_16_CARACTERES" --project TeaLearning.Api
```
*(En entornos de producción o contenedores, define la variable de entorno `Email__Password` con la clave de 16 caracteres sin espacios).*

### 3. Aplicar Migraciones a la Base de Datos
```bash
dotnet ef database update --project TeaLearning.Data --startup-project TeaLearning.Api
```

### 4. Iniciar la Aplicación
```bash
dotnet run --project TeaLearning.Api --launch-profile http
```
* API local: `http://localhost:5264`
* Swagger UI: `http://localhost:5264/swagger`

---

## 👤 Credenciales de Prueba (Semillero Automático)

El semillero (`DatabaseSeeder`) crea —o migra automáticamente de forma idempotente desde los antiguos correos `@tealearning.com`— las siguientes cuentas para que los roles con recuperación por correo apunten a buzones reales mediante subaddressing (`+`) en Gmail:

| Rol | Correo Electrónico | Contraseña | Recuperación de Contraseña | Perfil Asociado |
|---|---|---|---|---|
| **Administrador** | `tealearning.rol+administrador@gmail.com` | `Admin123!*` | Por correo electrónico (`tealearning.rol@gmail.com`) | Administrador del Sistema |
| **Docente** | `tealearning.rol+docente@gmail.com` | `Docente123!*` | Por correo electrónico (`tealearning.rol@gmail.com`) | `TeacherProfile` y `Classroom` ("Aula Azul") |
| **Acudiente** | `tealearning.rol+acudiente@gmail.com` | `Acudiente123!*` | Por correo electrónico (`tealearning.rol@gmail.com`) | `GuardianProfile` con relación a Mateo Pérez |
| **Estudiante** | `estudiante@tealearning.com` | `Estudiante123!*` | Desde plataforma por su **Acudiente titular** | `StudentProfile` (`EST-TEA-001`) + `SensoryPreference` |

---

## 🔌 Catálogo Completo de Endpoints (Sprints 1, 2, 3 y 4)

### 1. Autenticación y Recuperación de Contraseña (`/api/auth`)
* `POST /api/auth/login`: Autentica credenciales y emite JWT + Refresh Token.
* `POST /api/auth/register`: Registra un usuario y crea su perfil según el rol.
* `POST /api/auth/refresh-token`: Renueva el token de acceso con refresh token.
* `POST /api/auth/forgot-password`: Solicita enlace de restablecimiento por correo electrónico (solo roles `Administrador`, `Docente` y `Acudiente`; respuesta genérica `200 OK` anti-enumeración; protegido con rate limit de 5 solicitudes / 15 min por IP).
* `POST /api/auth/reset-password`: Confirma el restablecimiento con token codificado en Base64Url, aplica nueva contraseña con hash BCrypt, invalida sesiones previas (`RefreshToken` y `SecurityStamp`) y envía correo de confirmación (protegido con rate limit de 10 solicitudes / 15 min por IP).

### 2. Gestión de Usuarios y Roles (`/api/users`)
* `GET /api/users`: Lista usuarios con sus roles y perfiles *(Rol: Administrador)*.
* `GET /api/users/{id}`: Detalle de usuario *(Rol: Administrador)*.
* `PUT /api/users/{id}/roles`: Actualiza roles RBAC con protección contra auto-democión *(Rol: Administrador)*.
* `PUT /api/users/{id}`: Actualiza datos y estado con protección contra auto-desactivación *(Rol: Administrador)*.
* `DELETE /api/users/{id}`: Elimina permanentemente usuario con protección contra auto-eliminación *(Rol: Administrador)*.

### 3. Rutina Visual Diaria y Perfil (`/api/students`)
* `GET /api/students/me/daily-routine`: Actividades del estudiante para hoy ordenadas pedagógicamente *(Rol: Estudiante)*.
* `GET /api/students/me/profile`: Perfil del estudiante autenticado *(Rol: Estudiante)*.
* `GET /api/students`: Consulta todos los estudiantes activos *(Rol: Docente o Administrador)*.
* `GET /api/students/me/achievements`: Logros, insignias personales y módulos concluidos (*HU-011*, *Rol: Estudiante*).

### 4. Configuración Sensorial Adaptativa (`/api/students/{id}/sensory-preferences`)
* `GET /api/students/{id}/sensory-preferences`: Consulta paleta, tipografía, contraste y escala *(Rol: Acudiente o Administrador)*.
* `PUT /api/students/{id}/sensory-preferences`: Actualiza configuración sensorial *(Rol: Acudiente o Administrador)*.

### 5. Módulos Temáticos de Aprendizaje (`/api/modules`) - *HU-007*
* `GET /api/modules`: Lista módulos activos con conteo de actividades *(Público/Autenticado)*.
* `GET /api/modules/{id}`: Detalle de módulo y sus actividades *(Público/Autenticado)*.
* `POST /api/modules`: Crear nuevo módulo pedagógico *(Rol: Administrador)*.
* `PUT /api/modules/{id}`: Actualizar módulo *(Rol: Administrador)*.
* `DELETE /api/modules/{id}`: Baja lógica del módulo *(Rol: Administrador)*.

### 6. Actividades Pedagógicas y Ejecución (`/api/activities`) - *HU-004 & HU-006*
* `GET /api/activities`: Consulta actividades con filtros por módulo, tipo y docente.
* `GET /api/activities/{id}`: Detalle de actividad con su estructura dinámica `ConfigJson`.
* `POST /api/activities`: Crear actividad con modelo híbrido *(Rol: Docente o Administrador)*.
* `PUT /api/activities/{id}`: Actualizar actividad *(Rol: Docente o Administrador)*.
* `DELETE /api/activities/{id}`: Eliminar actividad *(Rol: Docente o Administrador)*.
* `POST /api/activities/{assignmentId}/start`: Inicia la actividad, cambia estado a `EnProgreso` y entrega el perfil sensorial adaptativo *(Rol: Estudiante)*.
* `POST /api/activities/{assignmentId}/submit`: Registra telemetría (tiempo, aciertos, intentos fallidos) y actualiza estado a `Completada` *(Rol: Estudiante)*.

### 7. Asignación de Actividades (`/api/assignments`) - *HU-005*
* `POST /api/assignments`: Asigna una actividad a un estudiante individual o en lote a un aula completa *(Rol: Docente o Administrador)*.
* `GET /api/assignments`: Lista asignaciones filtrando por estudiante, aula o estado *(Rol: Docente, Administrador o Acudiente)*.
* `GET /api/assignments/{id}`: Detalle de la asignación.
* `DELETE /api/assignments/{id}`: Cancelar o eliminar asignación *(Rol: Docente o Administrador)*.

### 8. Panel Analítico Docente (`/api/analytics`) - *HU-008*
* `GET /api/analytics/teacher/students/{id}/summary`: Analítica individual por estudiante (tasa aciertos global, tiempos por tipo de actividad, curva de intentos y evolución semanal) *(Rol: Docente o Administrador)*.
* `GET /api/analytics/teacher/classes/{classId}`: Analítica consolidada por aula con listado comparativo de desempeño de estudiantes *(Rol: Docente o Administrador)*.

### 9. Monitoreo y Seguridad para el Acudiente (`/api/guardian`) - *HU-009 & HU-012*
* `GET /api/guardian/students/{studentId}/progress`: Resumen de avance, tiempo total invertido, historial de ejecuciones y mensaje pedagógico empático *(Rol: Acudiente autorizado o Administrador)*.
* `GET /api/guardian/students/{studentId}/activity-history`: Historial paginado de ejecuciones con resultados y telemetría.
* `GET /api/guardian/students/{studentId}/observations`: Consulta paginada y filtrada cronológicamente de observaciones docentes para familias (*HU-012*, *Rol: Acudiente autorizado o Administrador*).
* `POST /api/guardian/students/{studentId}/reset-password`: Restablece directamente la contraseña de un estudiante vinculado e invalida sus sesiones activas *(Exclusivo para el Rol: Acudiente titular del estudiante)*.

### 10. Bitácora Pedagógica (`/api/students/{id}/pedagogical-notes`) - *HU-010*
* `POST /api/students/{id}/pedagogical-notes`: Registro de observación cualitativa sobre el estudiante *(Rol: Docente o Administrador)*.
* `GET /api/students/{id}/pedagogical-notes`: Consulta cronológica de notas cualitativas de la bitácora *(Rol: Docente, Administrador o Acudiente del estudiante)*.
* `PUT /api/log-entries/{id}`: Edición de notas cualitativas *(Rol: Docente o Administrador)*.
* `DELETE /api/log-entries/{id}`: Eliminación lógica de notas cualitativas *(Rol: Docente o Administrador)*.