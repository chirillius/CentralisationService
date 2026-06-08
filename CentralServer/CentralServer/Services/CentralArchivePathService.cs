using CentralServer.Models;

namespace CentralServer.Services;

public sealed class CentralArchivePathService
{
    private readonly IWebHostEnvironment _environment;

    public CentralArchivePathService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public string BuildRootPath(string rootPath)
    {
        return Path.Combine(_environment.ContentRootPath, SanitizeSegment(rootPath));
    }

    public string BuildMotionVideoDirectory(string rootPath, RemoteCameraState camera, DateTime capturedAt)
    {
        return Path.Combine(
            BuildStoreRootPath(rootPath, camera),
            "videos",
            capturedAt.ToString("yyyy-MM-dd"),
            SanitizeSegment(camera.CameraName),
            "videos");
    }

    public string BuildDefectImagesDirectory(
        string rootPath,
        RemoteCameraState camera,
        DateTime capturedAt,
        string defectName)
    {
        return Path.Combine(
            BuildStoreRootPath(rootPath, camera),
            "defects",
            capturedAt.ToString("yyyy-MM-dd"),
            SanitizeSegment(defectName),
            "images");
    }

    public string ToRelativePath(string rootPath, string fullPath)
    {
        return Path.GetRelativePath(BuildRootPath(rootPath), fullPath).Replace('\\', '/');
    }

    public string BuildFullPathFromRelative(string rootPath, string relativePath)
    {
        return Path.Combine(BuildRootPath(rootPath), relativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
    }

    public static string SanitizeSegment(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }

    private string BuildStoreRootPath(string rootPath, RemoteCameraState camera)
    {
        return Path.Combine(
            BuildRootPath(rootPath),
            SanitizeSegment(camera.CompanyKey),
            SanitizeSegment(camera.SiteKey));
    }
}
