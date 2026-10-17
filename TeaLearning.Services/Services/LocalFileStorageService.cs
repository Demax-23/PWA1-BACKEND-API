using System.Text;
using TeaLearning.Services.Interfaces;

namespace TeaLearning.Services.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _webRootPath;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp", ".gif"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif"
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

        if (!string.IsNullOrWhiteSpace(contentType) && !AllowedContentTypes.Contains(contentType.Trim()))
        {
            throw new ArgumentException("El tipo MIME del archivo no corresponde a un formato de imagen permitido.");
        }

        // Inspeccionar los primeros bytes para bloquear ejecutables o payloads HTML/SVG/Script disfrazados (polyglots)
        if (fileStream.CanSeek)
        {
            var headerBuffer = new byte[Math.Min(64, (int)fileStream.Length)];
            fileStream.Position = 0;
            int bytesRead = await fileStream.ReadAsync(headerBuffer.AsMemory(0, headerBuffer.Length), cancellationToken);
            fileStream.Position = 0;

            if (bytesRead >= 2 && headerBuffer[0] == 0x4D && headerBuffer[1] == 0x5A) // MZ (Windows PE executable)
            {
                throw new ArgumentException("El contenido binario del archivo no es una imagen válida.");
            }
            if (bytesRead >= 4 && headerBuffer[0] == 0x7F && headerBuffer[1] == 0x45 && headerBuffer[2] == 0x4C && headerBuffer[3] == 0x46) // ELF
            {
                throw new ArgumentException("El contenido binario del archivo no es una imagen válida.");
            }

            var headerText = Encoding.UTF8.GetString(headerBuffer, 0, bytesRead);
            string[] forbiddenSignatures = ["<svg", "<script", "<html", "<?xml", "<!doctype", "javascript:"];
            foreach (var sig in forbiddenSignatures)
            {
                if (headerText.Contains(sig, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("El contenido del archivo contiene etiquetas activas no permitidas en una imagen.");
                }
            }
        }

        var avatarsFolder = Path.GetFullPath(Path.Combine(_webRootPath, "uploads", "avatars"));
        if (!Directory.Exists(avatarsFolder))
        {
            Directory.CreateDirectory(avatarsFolder);
        }

        var fileName = $"avatar_{userId}_{DateTime.UtcNow.Ticks}{ext}";
        var fullPath = Path.GetFullPath(Path.Combine(avatarsFolder, fileName));

        using (var output = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await fileStream.CopyToAsync(output, cancellationToken);
        }

        return $"/uploads/avatars/{fileName}";
    }

    public Task<bool> DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return Task.FromResult(false);

        var avatarsRoot = Path.GetFullPath(Path.Combine(_webRootPath, "uploads", "avatars"));
        var cleanRelative = relativePath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_webRootPath, cleanRelative));

        // Prevenir Path Traversal: solo permitir eliminar archivos dentro de wwwroot/uploads/avatars
        if (!fullPath.StartsWith(avatarsRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

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
