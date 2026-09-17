# TeaLearning API - Backend & Base de Datos (TEA Nivel 1)

Backend RESTful y persistencia en SQL Server para una plataforma de apoyo pedagógico orientada a estudiantes diagnosticados con **Autismo Nivel 1 (Trastorno del Espectro Autista - TEA)**.

El sistema implementa interfaces accesibles con perfiles sensoriales personalizables, estructuración de rutinas predecibles y seguimiento pedagógico para docentes, acudientes y estudiantes.

---

## 🚀 Estado del Proyecto: Sprints 1 y 2 Finalizados

| Requerimiento | Puntos | Descripción | Estado |
|---|---|---|:---:|
| **HU-001 - Gestión de Usuarios y Roles** | 5 pts | Autenticación JWT, registro, login, refresh tokens y RBAC para roles `Administrador`, `Docente`, `Estudiante`, `Acudiente`. | ✅ Completado |
| **HU-002 - Rutina Visual de Actividades** | 5 pts | Endpoint `GET /api/students/me/daily-routine` con asignaciones del día ordenadas estrictamente por su secuencia pedagógica. | ✅ Completado |
| **HU-003 - Configuración Sensorial Adaptativa** | 5 pts | Configuración de paletas cromáticas, tipografía (ej. OpenDyslexic), supresión de animaciones, escala de fuentes, contraste y temporizadores. | ✅ Completado |
| **HU-007 - Módulos Temáticos de Aprendizaje** | 3 pts | CRUD administrativo de categorías y módulos pedagógicos (`GET`, `POST`, `PUT`, `DELETE /api/modules`). | ✅ Completado |
| **HU-004 - Parametrización de Actividades TEA** | 8 pts | Creación y parametrización de actividades con modelo híbrido (metadata relacional + estructura dinámica `ConfigJson`). | ✅ Completado |
| **HU-005 - Asignación Individual y por Aula** | 3 pts | Asignación de actividades a un estudiante específico o en lote a un aula completa con fechas, prioridad y orden secuencial. | ✅ Completado |
| **HU-006 - Ejecución Interactiva y Telemetría** | 8 pts | Flujo de inicio (`/start`) con entrega de adaptaciones sensoriales y registro de telemetría de desempeño (`/submit`). | ✅ Completado |

---

## 🛠️ Stack Tecnológico

* **Plataforma & Runtime:** .NET 9 (C# 13)
* **Framework Web:** ASP.NET Core Web API
* **Persistencia & ORM:** Entity Framework Core 9 (Code First)
* **Base de Datos:** Microsoft SQL Server (LocalDB / SQL Server Express / Developer)
* **Seguridad & Auth:** ASP.NET Core Identity (`IdentityDbContext`) con autenticación basada en tokens JWT (`JwtBearer`) y Claims estructurados.
* **Documentación Interactiva:** Swagger / OpenAPI (`Swashbuckle.AspNetCore`) con soporte para autorización Bearer Token.

---

## 🏛️ Arquitectura de la Solución (Clean Layered Architecture)

```text
TeaLearningSystem/
├── TeaLearning.Api/           # Capa de Entrada / Controladores RESTful
│   ├── Controllers/           # Auth, Users, Students, SensoryPreferences, Modules, Activities, Assignments
│   ├── Middlewares/           # ExceptionHandlingMiddleware (ProblemDetails / ApiResponse unificado)
│   ├── Extensions/            # Inyección de dependencias (DI), Swagger, JWT, Identity y CORS
│   └── Program.cs             # Pipeline HTTP y punto de entrada de la aplicación
│
├── TeaLearning.Services/      # Capa de Lógica de Negocio
│   ├── Common/                # ApiResponse<T> unificado
│   ├── DTOs/                  # DTOs de Auth, Users, Students, Sensory, Modules, Activities, Assignments, Execution
│   ├── Interfaces/            # IAuthService, IUserService, IModuleService, IActivityService, IAssignmentService, etc.
│   └── Services/              # Implementaciones de lógica pedagógica, validación JSON y telemetría
│
├── TeaLearning.Data/          # Capa de Persistencia y Acceso a Datos
│   ├── Context/               # ApplicationDbContext con Identity y DbSets de dominio
│   ├── Configurations/        # Mapeos Fluent API para entidades y relaciones sin ciclos de cascada
│   ├── Repositorios/          # Repositorios genéricos y especializados
│   ├── Migrations/            # Historial de migraciones de EF Core
│   └── Seeders/               # DatabaseSeeder para inicialización automática
│
└── TeaLearning.Core/          # Capa de Dominio (Modelos Puros)
    ├── Common/                # BaseEntity (Id, CreatedAt, UpdatedAt, IsActive)
    ├── Entities/              # Entidades del dominio completo
    └── Enums/                 # UserRole, ModoContraste, TipoActividad, EstadoAsignacion, PrioridadActividad
```

---

## 📋 Estructura de Respuestas de la API

Todas las respuestas del sistema siguen el formato estandarizado:

```json
{
  "success": true,
  "message": "Operación ejecutada con éxito",
  "data": { ... },
  "errors": []
}
```

---

## ⚙️ Configuración y Ejecución Local

### Prerrequisitos
* [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (o .NET 10 compatible)
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

### 2. Aplicar Migraciones a la Base de Datos
```bash
dotnet ef database update --project TeaLearning.Data --startup-project TeaLearning.Api
```

### 3. Iniciar la Aplicación
```bash
dotnet run --project TeaLearning.Api --launch-profile http
```
* API local: `http://localhost:5264`
* Swagger UI: `http://localhost:5264/swagger`

---

## 👤 Credenciales de Prueba (Semillero Automático)

| Rol | Correo Electrónico | Contraseña | Perfil Asociado |
|---|---|---|---|
| **Administrador** | `admin@tealearning.com` | `Admin123!*` | Administrador del Sistema |
| **Docente** | `docente@tealearning.com` | `Docente123!*` | `TeacherProfile` y `Classroom` ("Aula Azul") |
| **Acudiente** | `acudiente@tealearning.com` | `Acudiente123!*` | `GuardianProfile` con relación al estudiante |
| **Estudiante** | `estudiante@tealearning.com` | `Estudiante123!*` | `StudentProfile` (`EST-TEA-001`) + `SensoryPreference` |

---

## 🔌 Catálogo Completo de Endpoints (Sprints 1 y 2)

### 1. Autenticación (`/api/auth`)
* `POST /api/auth/login`: Autentica credenciales y emite JWT + Refresh Token.
* `POST /api/auth/register`: Registra un usuario y crea su perfil según el rol.
* `POST /api/auth/refresh-token`: Renueva el token de acceso con refresh token.

### 2. Gestión de Usuarios y Roles (`/api/users`)
* `GET /api/users`: Lista usuarios con sus roles y perfiles *(Rol: Administrador)*.
* `GET /api/users/{id}`: Detalle de usuario *(Rol: Administrador)*.
* `PUT /api/users/{id}/roles`: Actualiza roles RBAC *(Rol: Administrador)*.

### 3. Rutina Visual Diaria (`/api/students`)
* `GET /api/students/me/daily-routine`: Actividades del estudiante para hoy ordenadas pedagógicamente *(Rol: Estudiante)*.
* `GET /api/students/me/profile`: Perfil del estudiante autenticado *(Rol: Estudiante)*.

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

---

## 📌 Próximo Alcance: Sprint 3 (Analítica Cognitiva y Seguimiento)

* **HU-008:** Panel Analítico del Docente con métricas agregadas por estudiante y por aula (`/api/analytics/teacher/...`).
* **HU-009:** Monitoreo del Acudiente con progreso, tiempo invertido y evolución (`/api/guardian/...`).
* **HU-010:** Bitácora Pedagógica (CRUD de notas cualitativas docentes) (`/api/students/{id}/pedagogical-notes`).
* **HU-011:** Visualización de Logros No Competitivos del Estudiante (`/api/students/me/achievements`).