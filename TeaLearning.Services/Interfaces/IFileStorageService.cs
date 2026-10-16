namespace TeaLearning.Services.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveAvatarAsync(Guid userId, Stream fileStream, string originalFileName, string contentType, CancellationToken cancellationToken = default);
    Task<bool> DeleteFileAsync(string relativePath, CancellationToken cancellationToken = default);
}
