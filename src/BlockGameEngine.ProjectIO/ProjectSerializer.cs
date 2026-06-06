using System.Text.Json;
using BlockGameEngine.Runtime;

namespace BlockGameEngine.ProjectIO;

public sealed class ProjectSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public async Task SaveAsync(ProjectModel project, string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, project, Options, cancellationToken);
    }

    public async Task<ProjectModel> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var project = await JsonSerializer.DeserializeAsync<ProjectModel>(stream, Options, cancellationToken);
        return project ?? throw new InvalidDataException("Project file is empty.");
    }

    public static string GetProjectDirectory(string projectPath)
        => Path.GetDirectoryName(Path.GetFullPath(projectPath)) ?? Environment.CurrentDirectory;

    public static string GetAssetsDirectory(string projectPath)
        => Path.Combine(GetProjectDirectory(projectPath), "assets");

    public static async Task<string> ImportAssetAsync(string projectPath, string sourceFilePath, CancellationToken cancellationToken = default)
    {
        var assetsDir = GetAssetsDirectory(projectPath);
        Directory.CreateDirectory(assetsDir);
        var fileName = Path.GetFileName(sourceFilePath);
        var destPath = Path.Combine(assetsDir, fileName);
        await using var source = File.OpenRead(sourceFilePath);
        await using var dest = File.Create(destPath);
        await source.CopyToAsync(dest, cancellationToken);
        return Path.Combine("assets", fileName).Replace('\\', '/');
    }

    public static void CopyAssetsToDirectory(string projectPath, string outputDirectory)
    {
        var assetsDir = GetAssetsDirectory(projectPath);
        if (!Directory.Exists(assetsDir))
        {
            return;
        }

        var destAssets = Path.Combine(outputDirectory, "assets");
        Directory.CreateDirectory(destAssets);
        foreach (var file in Directory.GetFiles(assetsDir))
        {
            File.Copy(file, Path.Combine(destAssets, Path.GetFileName(file)), overwrite: true);
        }
    }

    public static string ResolveAssetPath(string baseDirectory, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return "";
        }

        return Path.Combine(baseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
