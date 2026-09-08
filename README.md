# TeaLearning API - Backend & Base de Datos (TEA Nivel 1)

Backend RESTful y persistencia en SQL Server para una plataforma de apoyo pedagógico orientada a estudiantes diagnosticados con **Autismo Nivel 1 (Trastorno del Espectro Autista - TEA)**.

El sistema implementa interfaces accesibles con perfiles sensoriales personalizables, estructuración de rutinas predecibles y seguimiento pedagógico para docentes, acudientes y estudiantes.

---

## 🚀 Estado del Proyecto: Sprint 1 Finalizado

Actualmente el proyecto cuenta con el **Sprint 1** implementado, probado y verificado en su totalidad:

| Requerimiento | Puntos | Descripción | Estado |
|---|---|---|:---:|
| **HU-001 - Gestión de Usuarios y Roles** | 5 pts | Autenticación JWT, registro, login, refresh tokens y RBAC para roles `Administrador`, `Docente`, `Estudiante`, `Acudiente`. | ✅ Completado |
| **HU-002 - Rutina Visual de Actividades** | 5 pts | Endpoint `GET /api/students/me/daily-routine` con asignaciones del día ordenadas estrictamente por su secuencia pedagógica. | ✅ Completado |
| **HU-003 - Configuración Sensorial Adaptativa** | 5 pts | Configuración de paletas cromáticas, tipografía (ej. OpenDyslexic), supresión de animaciones, escala de fuentes, contraste y temporizadores. | ✅ Completado |

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

La solución se divide en cuatro capas desacopladas:

```text
TeaLearningSystem/
├── TeaLearning.Api/           # Capa de Entrada / Presentación
│   ├── Controllers/           # Controladores RESTful (Auth, Users, Students, SensoryPreferences)
│   ├── Middlewares/           # Middleware global de excepciones estandarizadas
│   ├── Extensions/            # Inyección de dependencias (DI), Swagger, JWT, Identity y CORS
│   └── Program.cs             # Pipeline HTTP y punto de entrada de la aplicación
│
├── TeaLearning.Services/      # Capa de Lógica de Negocio
│   ├── Common/                # ApiResponse<T> unificado
│   ├── DTOs/                  # Data Transfer Objects para Auth, Users, Students, Sensory
│   ├── Interfaces/            # Contratos de servicios (IAuthService, IUserService, etc.)
│   └── Services/              # Implementaciones de negocio y proveedor JWT
│
├── TeaLearning.Data/          # Capa de Persistencia y Acceso a Datos
│   ├── Context/               # ApplicationDbContext configurado con Identity
│   ├── Configurations/        # Mapeos Fluent API para entidades y relaciones sin ciclos
│   ├── Repositorios/          # Patrón repositorio genérico y especializados
│   ├── Migrations/            # Historial de migraciones de EF Core
│   └── Seeders/               # DatabaseSeeder para inicialización automática
│
└── TeaLearning.Core/          # Capa de Dominio (Modelos Puros)
    ├── Common/                # BaseEntity (Id, CreatedAt, UpdatedAt, IsActive)
    ├── Entities/              # Entidades del dominio completo
    └── Enums/                 # Enumeraciones (UserRole, ModoContraste, TipoActividad, etc.)
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

### 2. Cadena de Conexión (`appsettings.json`)
La API está preconfigurada para conectarse a LocalDB:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=TeaLearningDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

### 3. Aplicar Migraciones a la Base de Datos
```bash
dotnet ef database update --project TeaLearning.Data --startup-project TeaLearning.Api
```

### 4. Iniciar la Aplicación
```bash
dotnet run --project TeaLearning.Api --launch-profile http
```
* La API estará disponible en: `http://localhost:5264`
* Documentación interactiva Swagger: `http://localhost:5264/swagger`

---

## 👤 Credenciales de Prueba (Semillero Automático)

Al iniciar la aplicación por primera vez, el `DatabaseSeeder` crea automáticamente los roles y usuarios de prueba:

| Rol | Correo Electrónico | Contraseña | Perfil Asociado |
|---|---|---|---|
| **Administrador** | `admin@tealearning.com` | `Admin123!*` | Perfil de Administrador del Sistema |
| **Docente** | `docente@tealearning.com` | `Docente123!*` | `TeacherProfile` y `Classroom` ("Aula Azul") |
| **Acudiente** | `acudiente@tealearning.com` | `Acudiente123!*` | `GuardianProfile` con relación al estudiante |
| **Estudiante** | `estudiante@tealearning.com` | `Estudiante123!*` | `StudentProfile` (`EST-TEA-001`) + `SensoryPreference` |

---

## 🔌 Catálogo de Endpoints Implementados (Sprint 1)

### 1. Autenticación (`/api/auth`)
* `POST /api/auth/login`: Autentica credenciales y emite JWT + Refresh Token.
* `POST /api/auth/register`: Registra un usuario y crea automáticamente su perfil asociado según el rol (`Estudiante`, `Docente`, `Acudiente`).
* `POST /api/auth/refresh-token`: Renueva el token de acceso mediante un refresh token vigente.

### 2. Gestión de Usuarios y Roles (`/api/users`)
* `GET /api/users`: Lista todos los usuarios con sus roles y perfiles vinculados *(Requiere Rol: Administrador)*.
* `GET /api/users/{id}`: Obtiene el detalle de un usuario específico *(Requiere Rol: Administrador)*.
* `PUT /api/users/{id}/roles`: Actualiza los roles RBAC de un usuario *(Requiere Rol: Administrador)*.

### 3. Rutina Visual Diaria (`/api/students`)
* `GET /api/students/me/daily-routine`: Retorna las actividades asignadas al estudiante para hoy, ordenadas estrictamente por `ordenSecuencial` e indicando su estado (`Pendiente`, `EnProgreso`, `Completada`) *(Requiere Rol: Estudiante)*.
* `GET /api/students/me/profile`: Retorna la información pedagógica del estudiante autenticado *(Requiere Rol: Estudiante)*.

### 4. Configuración Sensorial Adaptativa (`/api/students/{id}/sensory-preferences`)
* `GET /api/students/{id}/sensory-preferences`: Consulta la configuración sensorial (paleta, tipografía, audio, animaciones, contraste, escala) *(Requiere Rol: Acudiente o Administrador)*.
* `PUT /api/students/{id}/sensory-preferences`: Actualiza las preferencias sensoriales del estudiante *(Requiere Rol: Acudiente o Administrador)*.

---

## 📌 Próximo Alcance: Sprint 2 (Gestión Pedagógica)

* **HU-007:** CRUD de Módulos Temáticos de Aprendizaje (`/api/modules`).
* **HU-004:** Creación y parametrización de actividades con metadata relacional y columna híbrida `ConfigJson` (`/api/activities`).
* **HU-005:** Asignación individual y por aula de actividades con fechas, orden secuencial y prioridad (`/api/assignments`).
* **HU-006:** Ejecución de actividades y registro de telemetría de respuesta, aciertos e intentos (`/api/activities/{id}/start`, `/api/activities/{id}/submit`).