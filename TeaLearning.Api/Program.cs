using TeaLearning.Api.Extensions;
using TeaLearning.Api.Middlewares;
using TeaLearning.Data.Seeders;

var builder = WebApplication.CreateBuilder(args);

// Configurar controladores de API
builder.Services.AddControllers();

// Configurar capas de infraestructura, base de datos, JWT, Identity y Swagger
builder.Services.AddInfrastructureServices(builder.Configuration);

var app = builder.Build();

// Middleware global de excepciones estandarizadas
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Habilitar Swagger y Swagger UI
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TeaLearning API v1");
    c.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();

// Habilitar CORS
app.UseCors("AllowAllPolicy");

// Autenticación y Autorización RBAC
app.UseAuthentication();
app.UseAuthorization();

// Mapeo de Controladores RESTful
app.MapControllers();

// Inicialización y Semillero de la Base de Datos
try
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Iniciando verificación y sembrado de la base de datos...");
    await DatabaseSeeder.SeedAsync(app.Services);
    logger.LogInformation("Base de datos y semillero inicializados correctamente.");
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Error al inicializar la base de datos y ejecutar el semillero.");
}

app.Run();
