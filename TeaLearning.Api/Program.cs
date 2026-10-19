using Microsoft.EntityFrameworkCore;
using TeaLearning.Api.Extensions;
using TeaLearning.Api.Middlewares;
using TeaLearning.Data.Context;
using TeaLearning.Data.Seeders;

var builder = WebApplication.CreateBuilder(args);

// Configurar controladores de API
builder.Services.AddControllers();

// Configurar capas de infraestructura, base de datos, JWT, Identity y Swagger
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Soporte de Proxy Inverso (Azure App Service / Cloudflare) al inicio del pipeline
app.UseForwardedHeaders();

// Middleware global de excepciones estandarizadas (RFC 7807)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Habilitar Swagger y Swagger UI únicamente en entorno de Desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TeaLearning API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

// Habilitar archivos estáticos (para avatares y recursos multimedia en wwwroot)
app.UseStaticFiles();

// Habilitar CORS restringido a los orígenes configurados (Cloudflare Pages + Localhost)
app.UseCors("FrontendPolicy");

// Limitación de solicitudes por IP (autenticación y restablecimiento de contraseña)
app.UseRateLimiter();

// Autenticación y Autorización RBAC
app.UseAuthentication();
app.UseAuthorization();

// Endpoint ligero de salud para el sondeo Always On de Azure App Service
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "TeaLearning.Api",
    environment = app.Environment.EnvironmentName,
    timestamp = DateTime.UtcNow
}))
.AllowAnonymous()
.DisableRateLimiting();

// Mapeo de Controladores RESTful
app.MapControllers();

// Auto-Migración Segura y Semillero Idempotente (LocalDB y Azure SQL Database)
try
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    logger.LogInformation("Aplicando migraciones pendientes en SQL Server / Azure SQL...");
    await dbContext.Database.MigrateAsync();

    if (builder.Configuration.GetValue<bool>("SeedDatabaseOnStartup", true))
    {
        logger.LogInformation("Ejecutando semillero idempotente de roles y cuentas base...");
        await DatabaseSeeder.SeedAsync(app.Services);
    }

    logger.LogInformation("Base de datos y semillero inicializados correctamente.");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Error al aplicar migraciones o ejecutar el semillero en el arranque.");
}

app.Run();
