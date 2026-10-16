using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeaLearning.Core.Entities;
using TeaLearning.Data.Context;
using TeaLearning.Services.Services;

namespace TeaLearning.Tests;

public class UserAvatarTests : IDisposable
{
    private readonly string _testWebRoot;

    public UserAvatarTests()
    {
        _testWebRoot = Path.Combine(Path.GetTempPath(), "TeaLearning_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testWebRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testWebRoot))
            {
                Directory.Delete(_testWebRoot, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task SaveAvatarAsync_WithValidImage_ShouldSaveFileAndReturnRelativeUrl()
    {
        // Arrange
        var storage = new LocalFileStorageService(_testWebRoot);
        var userId = Guid.NewGuid();
        var fakeImageBytes = Encoding.UTF8.GetBytes("fake-png-content");
        using var stream = new MemoryStream(fakeImageBytes);

        // Act
        var relativeUrl = await storage.SaveAvatarAsync(userId, stream, "profile.png", "image/png");

        // Assert
        Assert.NotNull(relativeUrl);
        Assert.StartsWith("/uploads/avatars/avatar_" + userId, relativeUrl);
        Assert.EndsWith(".png", relativeUrl);

        var physicalPath = Path.Combine(_testWebRoot, relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physicalPath));
    }

    [Fact]
    public async Task SaveAvatarAsync_WithInvalidExtension_ShouldThrowArgumentException()
    {
        // Arrange
        var storage = new LocalFileStorageService(_testWebRoot);
        var userId = Guid.NewGuid();
        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.SaveAvatarAsync(userId, stream, "malicious.exe", "application/octet-stream"));
    }

    [Fact]
    public async Task DeleteFileAsync_WithExistingFile_ShouldRemoveIt()
    {
        // Arrange
        var storage = new LocalFileStorageService(_testWebRoot);
        var userId = Guid.NewGuid();
        using var stream = new MemoryStream(new byte[] { 10, 20, 30 });
        var relativeUrl = await storage.SaveAvatarAsync(userId, stream, "photo.webp", "image/webp");

        var physicalPath = Path.Combine(_testWebRoot, relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physicalPath));

        // Act
        var deleted = await storage.DeleteFileAsync(relativeUrl);

        // Assert
        Assert.True(deleted);
        Assert.False(File.Exists(physicalPath));
    }
}
