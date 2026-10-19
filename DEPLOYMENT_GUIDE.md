# Guía de Despliegue en Producción — Sendero / TeaLearning (PWA1)

**Arquitectura Objetivo:**
- **Backend API (.NET 9 + EF Core 9):** Azure App Service (Plan Basic B1) + Azure SQL Database.
- **Frontend Estático (Vanilla HTML5/CSS3/ES6+):** Cloudflare Pages (Red Global Edge).
- **Compatibilidad Dual:** Desarrollo local (`http://127.0.0.1:5500` + `http://localhost:5264`) y Producción 100% simultáneos sin alterar la lógica de negocio ni las pautas de accesibilidad **WCAG 2.1 AA/AAA**.

---

## 1. Tabla Exacta de Variables de Entorno para Azure App Service

En el portal de Azure, dirígete a tu **App Service** → **Settings (Configuración)** → **Environment variables (Variables de entorno)** → pestaña **App settings** y agrega las siguientes variables usando la convención de **doble guion bajo (`__`)** para jerarquías JSON en Linux/Windows:

| Nombre de la Variable (Azure App Settings) | Valor de Ejemplo / Producción | Propósito Técnico |
| :--- | :--- | :--- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Activa `appsettings.Production.json`, oculta Swagger UI y enmascara detalles internos en `ProblemDetails` RFC 7807. |
| `ConnectionStrings__DefaultConnection` | `Server=tcp:tealearning-sql.database.windows.net,1433;Initial Catalog=TeaLearningDb;Persist Security Info=False;User ID=teaadmin;Password=<TU_PASSWORD_SQL>;MultipleActiveResultSets=True;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;` | Cadena de conexión a **Azure SQL Database** (resiliente con `EnableRetryOnFailure` de 5 reintentos). |
| `Jwt__Key` | `TeaLearning_Prod_2026_UltraSecretSigningKey_Min32Bytes!#98` | Clave simétrica HMAC-SHA256 de producción ($\ge 32$ caracteres aleatorios). |
| `Jwt__SecretKey` | `TeaLearning_Prod_2026_UltraSecretSigningKey_Min32Bytes!#98` | Alias compatible de la clave de firma JWT para todos los servicios. |
| `Jwt__Issuer` | `TeaLearningApi` | Emisor validado en cada token de acceso y refresh token. |
| `Jwt__Audience` | `TeaLearningClients` | Audiencia validada en cada token de acceso y refresh token. |
| `Jwt__DurationInMinutes` | `60` | Duración del Access Token (minutos). |
| `Jwt__RefreshTokenDurationInDays` | `7` | Vigencia del Refresh Token (días). |
| `Email__Host` | `smtp.gmail.com` | Servidor SMTP para recuperación de contraseña (`MailKit` + STARTTLS). |
| `Email__Port` | `587` | Puerto SMTP con STARTTLS. |
| `Email__Username` | `tealearning.notificaciones@gmail.com` | Cuenta remitente autenticada en Gmail. |
| `Email__Password` | `<TU_APP_PASSWORD_DE_16_LETRAS_GMAIL>` | Contraseña de aplicación de Google (16 caracteres sin espacios). |
| `Email__SenderName` | `Sendero · TeaLearning` | Nombre visible en los correos de restablecimiento. |
| `Email__SenderAddress` | `tealearning.notificaciones@gmail.com` | Dirección remitente del sistema. |
| `Frontend__BaseUrl` | `https://sendero-tealearning.pages.dev` | Dominio público de Cloudflare Pages usado para construir el enlace `reset-password.html#email=...&token=...`. |
| `Cors__AllowedOrigins` | `https://sendero-tealearning.pages.dev,http://localhost:5500,http://127.0.0.1:5500` | Lista separada por comas de orígenes autorizados en CORS (admite también subdominios de preview). |
| `SeedDatabaseOnStartup` | `true` | Ejecuta migraciones (`MigrateAsync`) y el semillero idempotente (`DatabaseSeeder`) en el arranque. |

> **Nota:** Haz clic en **Apply (Aplicar)** y luego en **Confirm (Confirmar)** en la parte inferior de la hoja *Environment variables* para reiniciar el contenedor con las nuevas variables.

---

## 2. Checklist de Configuración: Azure SQL Database + Azure App Service (Plan Basic B1)

### A. Azure SQL Server & Database
- [ ] **Crear Servidor y Base de Datos:** Crea un servidor lógico (ej. `tealearning-sql.database.windows.net`) y una base de datos `TeaLearningDb` (nivel *Basic* o *General Purpose Serverless*).
- [ ] **Habilitar Firewall Interno de Azure:**
  1. Entra al recurso **SQL Server** (no la base de datos individual) → **Security (Seguridad)** → **Networking (Redes)**.
  2. En la pestaña **Public access**, marca la casilla:
     `[x] Allow Azure services and resources to access this server` (*Permitir que los servicios y recursos de Azure accedan a este servidor*).
  3. (Opcional) Agrega tu IP actual en **Firewall rules** si deseas inspeccionar la base de datos desde SSMS / Azure Data Studio.
  4. Haz clic en **Save (Guardar)**.
- [ ] **Verificación de Auto-Migración y Semillero:** No necesitas correr `dotnet ef database update` manualmente contra Azure. En cuanto arranque el App Service, `Program.cs` ejecutará `await dbContext.Database.MigrateAsync()` y `await DatabaseSeeder.SeedAsync(app.Services)` creando las tablas y las cuentas semilla:
  - **Administrador:** `tealearning.rol+administrador@gmail.com` / `Admin123!*`
  - **Docente:** `tealearning.rol+docente@gmail.com` / `Docente123!*`
  - **Acudiente:** `tealearning.rol+acudiente@gmail.com` / `Acudiente123!*`
  - **Estudiante:** `estudiante@tealearning.com` / `Estudiante123!*`

### B. Azure App Service (Plan Basic B1 — .NET 9)
- [ ] **Runtime Stack:** `.NET 9 (STS)` sobre Linux o Windows en plan **Basic B1** (evita el plan Free F1 durante la sustentación para no sufrir límites de cuota de CPU ni *cold starts*).
- [ ] **Activar Always On y Health Check:**
  1. Ve a **Settings (Configuración)** → **Configuration (Configuración)** → pestaña **General settings**.
  2. Activa **Always on** = `On` (evita que la API entre en suspensión antes de que los evaluadores escaneen el QR).
  3. Activa **HTTPS Only** = `On`.
  4. Ve a **Monitoring (Supervisión)** → **Health check** → activa **Enable** y escribe la ruta `/api/health`.
- [ ] **Conectar Repositorio en Deployment Center:**
  1. Ve a **Deployment (Implementación)** → **Deployment Center**.
  2. Selecciona **Source:** `GitHub`, autoriza tu cuenta y elige:
     - **Organization:** Tu usuario de GitHub
     - **Repository:** `PWA1-BACKEND-API`
     - **Branch:** `main`
  3. Deja la opción predeterminada **GitHub Actions** y haz clic en **Save**. Azure generará el workflow de compilación y despliegue automático de `TeaLearning.Api`.

---

## 3. Checklist de Configuración: Cloudflare Pages (`PWA1-FRONTEND`)

1. Inicia sesión en el panel de **Cloudflare** → **Workers & Pages** → **Create application** → pestaña **Pages** → **Connect to Git**.
2. Selecciona el repositorio **`PWA1-FRONTEND`**.
3. Configura los parámetros de compilación (**Set up builds and deployments**):
   - **Production branch:** `main`
   - **Framework preset:** `None` (Vanilla HTML5/CSS3/JS estático)
   - **Build command:** *(dejar completamente vacío)*
   - **Build output directory:** `/` *(o dejar vacío para publicar la raíz del repositorio)*
4. Haz clic en **Save and Deploy**.
5. Al finalizar (toma ~15 segundos), Cloudflare Pages te asignará un dominio HTTPS público (ej. `https://sendero-tealearning.pages.dev`) y aplicará automáticamente:
   - `_redirects`: Sirve `Interfaz_General.html` directamente en la raíz `/` con código `200`.
   - `_headers`: Aplica cabeceras `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy` y `Cache-Control: no-cache, no-store, must-revalidate` en todos los `.html`, `.js` y `.css`.
   - `404.html`: Redirige rutas inexistentes hacia `/error.html` conservando la ruta solicitada.

---

## 4. Secuencia Final de Prueba y Validación Pre-Sustentación (Código QR)

Sigue este orden exacto antes de generar el código QR para los evaluadores:

1. **Confirmar Subdominio de Azure App Service:**
   - Abre en tu navegador `https://<tu-app-service>.azurewebsites.net/api/health`.
   - Debes recibir un JSON `200 OK`:
     ```json
     {"status":"Healthy","service":"TeaLearning.Api","environment":"Production","timestamp":"..."}
     ```
2. **Actualizar `PRODUCTION_API_URL` en `PWA1-FRONTEND/api-client.js` (si el nombre en Azure cambió):**
   - Abre `PWA1-FRONTEND/api-client.js` y verifica la línea 7:
     ```javascript
     const PRODUCTION_API_URL = 'https://tealearning-api.azurewebsites.net/api';
     ```
   - Si tu App Service tiene otro nombre (ej. `https://sendero-api-2026.azurewebsites.net/api`), actualízalo, haz commit y `git push origin main` en `PWA1-FRONTEND`.
3. **Confirmar `Frontend__BaseUrl` y `Cors__AllowedOrigins` en Azure:**
   - Verifica que en Azure App Service ambas variables tengan el dominio exacto `https://<tu-proyecto>.pages.dev` (sin `/` al final).
4. **Prueba End-to-End desde un Teléfono Móvil (Datos Móviles / Wi-Fi externo):**
   - Ingresa a `https://<tu-proyecto>.pages.dev` desde el navegador del celular.
   - Verifica que cargue la portada accesible **Sendero (`Interfaz_General.html`)**.
   - Toca **Iniciar sesión** e ingresa con cada uno de los 4 roles (`Administrador`, `Docente`, `Acudiente`, `Estudiante`).
   - Prueba el flujo **¿Olvidaste tu contraseña?** con `tealearning.rol+docente@gmail.com` y verifica que el enlace recibido en Gmail apunte a `https://<tu-proyecto>.pages.dev/reset-password.html#email=...`.
   - Cierra sesión y presiona el botón "Atrás" del navegador móvil para confirmar que las cabeceras `no-store` de `_headers` y el listener `pageshow` bloquean el BFCache.
5. **Generar Código QR:**
   - Genera el código QR apuntando exclusivamente a `https://<tu-proyecto>.pages.dev`. Gracias al `UseForwardedHeaders()` configurado en `.NET 9`, cada teléfono que escanee el QR tendrá su propia cuota independiente en el `RateLimiter` por IP.
