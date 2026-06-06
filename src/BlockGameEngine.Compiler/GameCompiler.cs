using BlockGameEngine.Runtime;

namespace BlockGameEngine.Compiler;

public sealed class CompiledGamePackage
{
    public string FormatVersion { get; set; } = "1.0";
    public DateTimeOffset CompiledAt { get; set; } = DateTimeOffset.UtcNow;
    public ProjectModel Project { get; set; } = new();
}

public sealed class GameCompiler
{
    public CompiledGamePackage Compile(ProjectModel project)
    {
        if (project.Scenes.Count == 0)
        {
            throw new InvalidOperationException("Project must contain at least one scene.");
        }

        if (string.IsNullOrWhiteSpace(project.StartSceneName))
        {
            project.StartSceneName = project.Scenes[0].Name;
        }

        return new CompiledGamePackage
        {
            Project = project
        };
    }
}
