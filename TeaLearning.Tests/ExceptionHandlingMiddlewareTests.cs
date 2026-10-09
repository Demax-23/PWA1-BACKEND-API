using System.IO;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using TeaLearning.Api.Middlewares;

namespace TeaLearning.Tests;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenExceptionThrown_ReturnsRfc7807ProblemDetails()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/api/test-failure";

        var middleware = new ExceptionHandlingMiddleware(
            next: (innerContext) => throw new InvalidOperationException("Simulated unexpected failure"),
            logger: NullLogger<ExceptionHandlingMiddleware>.Instance);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("https://tools.ietf.org/html/rfc7231#section-6.6.1", root.GetProperty("type").GetString());
        Assert.Equal("/api/test-failure", root.GetProperty("instance").GetString());
        Assert.True(root.TryGetProperty("title", out _));
        Assert.True(root.TryGetProperty("detail", out _));
        Assert.True(root.TryGetProperty("traceId", out _));
        Assert.False(root.GetProperty("success").GetBoolean());
    }
}
