using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeaLearning.Api.Converters;
using TeaLearning.Api.Extensions;
using TeaLearning.Api.Middlewares;
using TeaLearning.Data.Context;
using TeaLearning.Data.Seeders;
using TeaLearning.Services.Common;

var builder = WebApplication.CreateBuilder(args);

// Configurar controladores de API con respuesta de validación unificada (ApiResponse) y soporte decimal flexible ('.' y ',')
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new FlexibleDecimalJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new FlexibleNullableDecimalJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new NullableUtcDateTimeJsonConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) ? "Valor de entrada inválido." : e.ErrorMessage)
                .Distinct()
                .ToList();

            var message = errors.Count > 0 ? errors[0] : "Datos de entrada inválidos.";
            return new BadRequestObjectResult(ApiResponse<object>.Fail(message, errors));
        };
    });

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

// Compuerta de sincronización: permite que Kestrel abra el puerto de inmediato para el Health Check de Azure App Service
// mientras encola de forma segura las peticiones /api/* (salvo /api/health) hasta finalizar migraciones y semillero.
var dbInitializationTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

// Endpoint ligero de salud para el sondeo Always On de Azure App Service
app.MapGet("/api/health", () => Results.Ok(new
{
    status = "Healthy",
    databaseInitialized = dbInitializationTcs.Task.IsCompleted,
    service = "TeaLearning.Api",
    environment = app.Environment.EnvironmentName,
    timestamp = DateTime.UtcNow
}))
.AllowAnonymous()
.DisableRateLimiting();

app.Use(async (context, next) =>
{
    if (!dbInitializationTcs.Task.IsCompleted &&
        context.Request.Path.StartsWithSegments("/api") &&
        !context.Request.Path.StartsWithSegments("/api/health"))
    {
        await Task.WhenAny(dbInitializationTcs.Task, Task.Delay(TimeSpan.FromSeconds(45), context.RequestAborted));
    }

    await next();
});

// Mapeo de Controladores RESTful
app.MapControllers();

// Auto-Migración Segura y Semillero Idempotente en segundo plano tras iniciar Kestrel
// Garantiza que app.Run() abra el puerto HTTP de inmediato para el Health Check de Azure App Service
var seedOnStartup = builder.Configuration.GetValue<bool>("SeedDatabaseOnStartup", true);
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            logger.LogInformation("Aplicando migraciones pendientes en SQL Server / Azure SQL en segundo plano...");
            await dbContext.Database.MigrateAsync();

            if (seedOnStartup)
            {
                logger.LogInformation("Ejecutando semillero idempotente de roles y cuentas base...");
                await DatabaseSeeder.SeedAsync(app.Services);
            }

            logger.LogInformation("Base de datos y semillero inicializados correctamente.");
        }
        catch (Exception ex)
        {
            using var errScope = app.Services.CreateScope();
            var logger = errScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "Error al aplicar migraciones o ejecutar el semillero en segundo plano.");
        }
        finally
        {
            dbInitializationTcs.TrySetResult(true);
        }
    });
});

app.Run();
