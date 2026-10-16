using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _webRootPath;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    public LocalFileStorageService(string? webRootPath = null)
    {
        _webRootPath = !string.IsNullOrWhiteSpace(webRootPath) 
            ? webRootPath 
            : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

    public async Task<string> SaveAvatarAsync(Guid userId, Stream fileStream, string originalFileName, string contentType, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new ArgumentException("El archivo seleccionado no tiene un formato de imagen permitido (.jpg, .jpeg, .png, .webp, .gif).");
        }

        var avatarsFolder = Path.Combine(_webRootPath, "uploads", "avatars");
        if (!Directory.Exists(avatarsFolder))
        {
            Directory.CreateDirectory(avatarsFolder);
        }

        var fileName = $"avatar_{userId}_{DateTime.UtcNow.Ticks}{ext}";
        var fullPath = Path.Combine(avatarsFolder, fileName);

        using (var output = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(output, cancellationToken);
        }

        return $"/uploads/avatars/{fileName}";
    }

    public Task<bool> DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return Task.FromResult(false);

        var cleanRelative = relativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_webRootPath, cleanRelative);

        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(false);
    }
}
