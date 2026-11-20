namespace CYHDocG.Repo.Interfaces;

public interface IImageCompressionService
{
    Task<(string relativePath, long originalSizeBytes, long compressedSizeBytes)> CompressPrescriptionImageAsync(Stream inputStream, string originalFileName, string webRootPath);
}
