using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using BlockGameEngine.Compiler;
using BlockGameEngine.ProjectIO;
using BlockGameEngine.Runtime;

namespace BlockGameEngine.Export;

public sealed class ExportResult
{
    public bool Success { get; init; }
    public string OutputPath { get; init; } = "";
    public string Message { get; init; } = "";
}

public interface IExportPipeline
{
    Task<ExportResult> ExportAsync(ProjectModel project, ExportOptions options, CancellationToken cancellationToken = default);
}

public sealed class ExportOptions
{
    public string RepositoryRoot { get; init; } = "";
    public string OutputDirectory { get; init; } = "";
    public bool BuildRelease { get; init; } = true;
    public string? ProjectFilePath { get; init; }
}

public sealed class WindowsExeExportPipeline : IExportPipeline
{
    public async Task<ExportResult> ExportAsync(ProjectModel project, ExportOptions options, CancellationToken cancellationToken = default)
    {
        var compiler = new GameCompiler();
        var package = compiler.Compile(project);
        var output = RequireOutput(options);
        Directory.CreateDirectory(output);

        var playerProject = Path.Combine(options.RepositoryRoot, "src", "BlockGameEngine.WindowsPlayer", "BlockGameEngine.WindowsPlayer.csproj");
        if (!File.Exists(playerProject))
        {
            await WritePackageAsync(package, output, cancellationToken);
            return new ExportResult
            {
                Success = false,
                OutputPath = output,
                Message = "Windows player project was not found. Package was generated only."
            };
        }

        if (options.BuildRelease)
        {
            var safeName = SanitizeName(project.Name);
            var result = await RunProcessAsync(
                "dotnet",
                $"publish \"{playerProject}\" -c Release --self-contained false -o \"{output}\" /p:AssemblyName={safeName} /p:ApplicationTitle=\"{project.Name}\"",
                options.RepositoryRoot,
                cancellationToken);

            if (result.ExitCode != 0)
            {
                await WritePackageAsync(package, output, cancellationToken);
                return new ExportResult
                {
                    Success = false,
                    OutputPath = output,
                    Message = result.Output
                };
            }

            var defaultExe = Path.Combine(output, "BlockGameEngine.WindowsPlayer.exe");
            var brandedExe = Path.Combine(output, $"{safeName}.exe");
            if (File.Exists(defaultExe) && defaultExe != brandedExe)
            {
                File.Move(defaultExe, brandedExe, overwrite: true);
            }
        }

        await WritePackageAsync(package, output, cancellationToken);

        if (!string.IsNullOrWhiteSpace(options.ProjectFilePath))
        {
            ProjectSerializer.CopyAssetsToDirectory(options.ProjectFilePath, output);
        }

        return new ExportResult
        {
            Success = true,
            OutputPath = output,
            Message = options.BuildRelease
                ? $"Windows exe export completed for \"{project.Name}\" v{project.Version}."
                : "Windows package export completed."
        };
    }

    private static string SanitizeName(string name)
    {
        var sanitized = Regex.Replace(name.Trim(), @"[^\w\-]", "_");
        return string.IsNullOrWhiteSpace(sanitized) ? "BlockGame" : sanitized;
    }

    private static string RequireOutput(ExportOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
        {
            throw new InvalidOperationException("OutputDirectory is required.");
        }

        return options.OutputDirectory;
    }

    private static async Task WritePackageAsync(CompiledGamePackage package, string output, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(output, "game.package.json"), json, cancellationToken);
    }

    internal static async Task<(int ExitCode, string Output)> RunProcessAsync(string fileName, string arguments, string workingDirectory, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        ConfigureIsolatedEnvironment(info, workingDirectory);

        using var process = Process.Start(info) ?? throw new InvalidOperationException("Could not start export process.");
        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken)
            + await process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return (process.ExitCode, output);
    }

    internal static void ConfigureIsolatedEnvironment(ProcessStartInfo info, string workingDirectory)
    {
        info.Environment["DOTNET_CLI_HOME"] = Path.Combine(workingDirectory, ".dotnet_home");
        info.Environment["NUGET_PACKAGES"] = Path.Combine(workingDirectory, ".nuget_packages");
        info.Environment["APPDATA"] = Path.Combine(workingDirectory, ".appdata");
        info.Environment["LOCALAPPDATA"] = Path.Combine(workingDirectory, ".localappdata");
        Directory.CreateDirectory(info.Environment["DOTNET_CLI_HOME"]!);
        Directory.CreateDirectory(info.Environment["NUGET_PACKAGES"]!);
        Directory.CreateDirectory(info.Environment["APPDATA"]!);
        Directory.CreateDirectory(info.Environment["LOCALAPPDATA"]!);
    }
}

public sealed class AndroidApkExportPipeline : IExportPipeline
{
    private static readonly string[] TemplateSourceFiles =
    [
        "BlockGameEngine.AndroidPlayer.Template.csproj",
        "MainActivity.cs",
        "GameView.cs",
        "AndroidInputAdapter.cs",
        "AndroidRenderer.cs"
    ];

    public async Task<ExportResult> ExportAsync(ProjectModel project, ExportOptions options, CancellationToken cancellationToken = default)
    {
        var output = string.IsNullOrWhiteSpace(options.OutputDirectory)
            ? throw new InvalidOperationException("OutputDirectory is required.")
            : options.OutputDirectory;
        Directory.CreateDirectory(output);

        var templateRoot = Path.Combine(options.RepositoryRoot, "src", "BlockGameEngine.AndroidPlayer.Template");
        if (!Directory.Exists(templateRoot))
        {
            throw new InvalidOperationException("Android player template was not found.");
        }

        foreach (var file in TemplateSourceFiles)
        {
            var source = Path.Combine(templateRoot, file);
            if (!File.Exists(source))
            {
                continue;
            }

            var destName = file == "BlockGameEngine.AndroidPlayer.Template.csproj"
                ? "BlockGame.AndroidPlayer.csproj"
                : file;
            var content = await File.ReadAllTextAsync(source, cancellationToken);
            content = ApplyBranding(content, project, destName);
            await File.WriteAllTextAsync(Path.Combine(output, destName), content, cancellationToken);
        }

        var assetsDir = Path.Combine(output, "Assets");
        Directory.CreateDirectory(assetsDir);
        var package = new GameCompiler().Compile(project);
        var json = JsonSerializer.Serialize(package, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(Path.Combine(assetsDir, "game.package.json"), json, cancellationToken);

        if (!string.IsNullOrWhiteSpace(options.ProjectFilePath))
        {
            var srcAssets = ProjectSerializer.GetAssetsDirectory(options.ProjectFilePath);
            if (Directory.Exists(srcAssets))
            {
                var destGameAssets = Path.Combine(assetsDir, "assets");
                Directory.CreateDirectory(destGameAssets);
                foreach (var assetFile in Directory.GetFiles(srcAssets))
                {
                    File.Copy(assetFile, Path.Combine(destGameAssets, Path.GetFileName(assetFile)), overwrite: true);
                }
            }
        }

        await File.WriteAllTextAsync(Path.Combine(output, "EXPORT_README.md"), BuildReadme(project), cancellationToken);

        var hasWorkload = await HasAndroidWorkloadAsync(options.RepositoryRoot, cancellationToken);
        if (hasWorkload && options.BuildRelease)
        {
            var csproj = Path.Combine(output, "BlockGame.AndroidPlayer.csproj");
            var result = await WindowsExeExportPipeline.RunProcessAsync(
                "dotnet",
                $"publish \"{csproj}\" -c Release -f net10.0-android",
                output,
                cancellationToken);

            return new ExportResult
            {
                Success = result.ExitCode == 0,
                OutputPath = output,
                Message = result.ExitCode == 0
                    ? $"Android APK export completed for \"{project.Name}\" v{project.Version}."
                    : result.Output
            };
        }

        return new ExportResult
        {
            Success = hasWorkload,
            OutputPath = output,
            Message = hasWorkload
                ? "Android project generated. Run dotnet publish -c Release -f net10.0-android to create an APK."
                : "Android project generated. Install the .NET Android workload, Android SDK, and JDK before building the APK."
        };
    }

    private static string ApplyBranding(string content, ProjectModel project, string destFileName)
    {
        if (destFileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            content = Regex.Replace(content, "<ApplicationId>.*</ApplicationId>", $"<ApplicationId>{project.ApplicationId}</ApplicationId>");
            content = Regex.Replace(content, "<ApplicationDisplayVersion>.*</ApplicationDisplayVersion>", $"<ApplicationDisplayVersion>{project.Version}</ApplicationDisplayVersion>");
            content = Regex.Replace(content, "<ApplicationVersion>.*</ApplicationVersion>", "<ApplicationVersion>1</ApplicationVersion>");
            content = content.Replace(
                "..\\BlockGameEngine.Runtime\\BlockGameEngine.Runtime.csproj",
                Path.Combine("..", "..", "src", "BlockGameEngine.Runtime", "BlockGameEngine.Runtime.csproj").Replace('\\', '/'));
            content = content.Replace(
                "..\\BlockGameEngine.Compiler\\BlockGameEngine.Compiler.csproj",
                Path.Combine("..", "..", "src", "BlockGameEngine.Compiler", "BlockGameEngine.Compiler.csproj").Replace('\\', '/'));
        }

        if (destFileName == "MainActivity.cs")
        {
            content = Regex.Replace(content, @"Label = ""[^""]*""", $"Label = \"{project.Name}\"");
        }

        return content;
    }

    private static string BuildReadme(ProjectModel project)
        => $"""
# Android Export — {project.Name}

This folder is generated by BlockGameEngine.

Build command:

```powershell
dotnet publish -c Release -f net10.0-android
```

Requirements:

- .NET Android workload
- Android SDK
- JDK

Application ID: `{project.ApplicationId}`
Version: `{project.Version}`
""";

    private static async Task<bool> HasAndroidWorkloadAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        var info = new ProcessStartInfo("dotnet", "workload list")
        {
            WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? Environment.CurrentDirectory : workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        WindowsExeExportPipeline.ConfigureIsolatedEnvironment(info, info.WorkingDirectory);

        using var process = Process.Start(info);
        if (process is null)
        {
            return false;
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode == 0 && output.Contains("android", StringComparison.OrdinalIgnoreCase);
    }
}
