using Microsoft.AspNetCore.Identity;
using TeaLearning.Core.Entities;
using TeaLearning.Services.Security;

namespace TeaLearning.Tests;

public class BcryptPasswordHasherTests
{
    private readonly BcryptPasswordHasher _hasher;
    private readonly ApplicationUser _testUser;

    public BcryptPasswordHasherTests()
    {
        _hasher = new BcryptPasswordHasher();
        _testUser = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = "docente@tealearning.com",
            UserName = "docente@tealearning.com"
        };
    }

    [Fact]
    public void HashPassword_ShouldProduceValidBcryptHashFormat()
    {
        // Arrange
        const string password = "PasswordSegura123!*";

        // Act
        string hash = _hasher.HashPassword(_testUser, password);

        // Assert
        Assert.NotNull(hash);
        Assert.StartsWith("$2", hash);
        Assert.Equal(60, hash.Length);
    }

    [Fact]
    public void HashPassword_DifferentSaltsForSamePassword()
    {
        // Arrange
        const string password = "PasswordSegura123!*";

        // Act
        string hash1 = _hasher.HashPassword(_testUser, password);
        string hash2 = _hasher.HashPassword(_testUser, password);

        // Assert
        Assert.NotEqual(hash1, hash2);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(_testUser, hash1, password));
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(_testUser, hash2, password));
    }

    [Fact]
    public void VerifyHashedPassword_WithCorrectPassword_ReturnsSuccess()
    {
        // Arrange
        const string password = "Docente123!*";
        string hash = _hasher.HashPassword(_testUser, password);

        // Act
        var result = _hasher.VerifyHashedPassword(_testUser, hash, password);

        // Assert
        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyHashedPassword_WithIncorrectPassword_ReturnsFailed()
    {
        // Arrange
        const string password = "Docente123!*";
        string hash = _hasher.HashPassword(_testUser, password);

        // Act
        var result = _hasher.VerifyHashedPassword(_testUser, hash, "PasswordIncorrecta");

        // Assert
        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void VerifyHashedPassword_WithLegacySha256OrCorruptHash_ReturnsFailedGracefully()
    {
        // Arrange: Ejemplo de hash PBKDF2/SHA256 tradicional de ASP.NET Core Identity
        const string legacySha256Hash = "AQAAAAIAAYagAAAAEO9l0mQkQ1q2Z3...fakelegacyhash";
        const string plainCorruptString = "not_a_valid_bcrypt_hash";

        // Act
        var resultLegacy = _hasher.VerifyHashedPassword(_testUser, legacySha256Hash, "Admin123!*");
        var resultCorrupt = _hasher.VerifyHashedPassword(_testUser, plainCorruptString, "Admin123!*");

        // Assert
        Assert.Equal(PasswordVerificationResult.Failed, resultLegacy);
        Assert.Equal(PasswordVerificationResult.Failed, resultCorrupt);
    }

    [Theory]
    [InlineData(null, "Admin123!*")]
    [InlineData("", "Admin123!*")]
    [InlineData("   ", "Admin123!*")]
    [InlineData("$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy", null)]
    [InlineData("$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy", "")]
    public void VerifyHashedPassword_WithNullOrWhitespaceValues_ReturnsFailed(string? hash, string? password)
    {
        // Act
        var result = _hasher.VerifyHashedPassword(_testUser, hash!, password!);

        // Assert
        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void HashPassword_WithNullOrEmptyPassword_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _hasher.HashPassword(_testUser, string.Empty));
        Assert.Throws<ArgumentNullException>(() => _hasher.HashPassword(_testUser, null!));
    }
}
