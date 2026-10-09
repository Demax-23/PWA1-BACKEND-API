using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TeaLearning.Api.Extensions;
using TeaLearning.Core.Entities;
using TeaLearning.Services.Security;

namespace TeaLearning.Tests;

public class IdentityBcryptIntegrationTests
{
    [Fact]
    public void DI_ResolvesBcryptPasswordHasher_As_IPasswordHasher()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Server=(localdb)\\mssqllocaldb;Database=TeaLearningTestDb;Trusted_Connection=True;",
                ["Jwt:SecretKey"] = "SecretKeyForTestingPurposesAtLeast32CharsLong!",
                ["Jwt:Issuer"] = "TestIssuer",
                ["Jwt:Audience"] = "TestAudience",
                ["Security:BcryptWorkFactor"] = "10"
            })
            .Build();

        services.AddLogging();
        services.AddInfrastructureServices(configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var hasher = serviceProvider.GetService<IPasswordHasher<ApplicationUser>>();

        // Assert
        Assert.NotNull(hasher);
        Assert.IsType<BcryptPasswordHasher>(hasher);

        // Act 2: Hash a password using the resolved hasher
        var testUser = new ApplicationUser { UserName = "admin@tealearning.com", Email = "admin@tealearning.com" };
        var hash = hasher.HashPassword(testUser, "Admin123!*");

        // Assert 2
        Assert.StartsWith("$2", hash);
        Assert.Equal(PasswordVerificationResult.Success, hasher.VerifyHashedPassword(testUser, hash, "Admin123!*"));
    }
}
