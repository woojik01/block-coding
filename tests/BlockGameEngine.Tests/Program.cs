using BlockGameEngine.Compiler;
using BlockGameEngine.Export;
using BlockGameEngine.ProjectIO;
using BlockGameEngine.Runtime;

var tests = new (string Name, Func<Task> Run)[]
{
    ("move block changes sprite position", TestMoveBlock),
    ("collision event changes variable", TestCollisionEvent),
    ("project serializer round trips", TestProjectRoundTrip),
    ("compiler rejects empty project", TestCompilerValidation),
    ("windows export writes package", TestWindowsExport),
    ("android export creates project files", TestAndroidExport),
    ("wait block pauses execution", TestWaitBlock),
    ("list blocks modify list", TestListBlocks),
    ("click event uses pointer input", TestClickEvent),
    ("editor layout round trips", TestEditorLayoutRoundTrip),
    ("export branding in android csproj", TestAndroidExportBranding)
};

var failures = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {test.Name}: {ex.Message}");
    }
}

if (failures > 0)
{
    Environment.ExitCode = 1;
}

static Task TestMoveBlock()
{
    var project = SampleProjectFactory.Create();
    var engine = new RuntimeEngine(project);
    var input = new MutableInputState();
    var player = engine.CurrentScene.Sprites[0];
    var before = player.X;

    input.SetKey("Right", true);
    engine.Tick(input, 0.016);

    Assert(player.X > before, "Player should move when Right is down.");
    return Task.CompletedTask;
}

static Task TestCollisionEvent()
{
    var project = SampleProjectFactory.Create();
    var scene = project.Scenes[0];
    scene.Sprites[0].X = scene.Sprites[1].X;
    scene.Sprites[0].Y = scene.Sprites[1].Y;

    var engine = new RuntimeEngine(project);
    engine.Tick(new MutableInputState(), 0.016);

    Assert(project.Variables["score"] > 0, "Collision should increment score.");
    return Task.CompletedTask;
}

static async Task TestProjectRoundTrip()
{
    var project = SampleProjectFactory.Create();
    project.Lists["items"] = ["a", "b"];
    project.EditorLayout.Nodes["test"] = new BlockNodeLayout { X = 10, Y = 20 };
    var path = Path.Combine(Path.GetTempPath(), $"blockgame-{Guid.NewGuid():N}.blockgame");
    var serializer = new ProjectSerializer();

    await serializer.SaveAsync(project, path);
    var loaded = await serializer.LoadAsync(path);

    Assert(loaded.Name == project.Name, "Project name should round trip.");
    Assert(loaded.Scenes[0].Sprites.Count == project.Scenes[0].Sprites.Count, "Sprites should round trip.");
    Assert(loaded.Lists["items"].Count == 2, "Lists should round trip.");
}

static Task TestCompilerValidation()
{
    var compiler = new GameCompiler();
    var failed = false;
    try
    {
        compiler.Compile(new ProjectModel());
    }
    catch (InvalidOperationException)
    {
        failed = true;
    }

    Assert(failed, "Compiler should reject a project with no scenes.");
    return Task.CompletedTask;
}

static async Task TestAndroidExport()
{
    var root = FindRepoRoot();
    var output = Path.Combine(Path.GetTempPath(), $"blockgame-android-{Guid.NewGuid():N}");
    var result = await new AndroidApkExportPipeline().ExportAsync(SampleProjectFactory.Create(), new ExportOptions
    {
        RepositoryRoot = root,
        OutputDirectory = output,
        BuildRelease = false
    });

    var csproj = await File.ReadAllTextAsync(Path.Combine(output, "BlockGame.AndroidPlayer.csproj"));
    Assert(File.Exists(Path.Combine(output, "BlockGame.AndroidPlayer.csproj")), "Android project file should exist.");
    Assert(File.Exists(Path.Combine(output, "Assets", "game.package.json")), "Compiled package should exist in Assets.");
    Assert(csproj.Contains("BlockGameEngine.Runtime", StringComparison.OrdinalIgnoreCase), "Android project should reference Runtime.");
    Assert(!string.IsNullOrWhiteSpace(result.Message), "Export should return a message.");
}

static async Task TestWindowsExport()
{
    var root = FindRepoRoot();
    var output = Path.Combine(Path.GetTempPath(), $"blockgame-windows-{Guid.NewGuid():N}");
    var result = await new WindowsExeExportPipeline().ExportAsync(SampleProjectFactory.Create(), new ExportOptions
    {
        RepositoryRoot = root,
        OutputDirectory = output,
        BuildRelease = false
    });

    Assert(File.Exists(Path.Combine(output, "game.package.json")), "Windows export should write a compiled package.");
    Assert(result.Success, "Windows package-only export should succeed.");
}

static Task TestWaitBlock()
{
    var project = SampleProjectFactory.Create();
    var player = project.Scenes[0].Sprites[0];
    player.Scripts.Clear();
    player.Scripts.Add(new BlockScriptModel
    {
        Root = new BlockModel
        {
            Kind = BlockKind.EventGameStart,
            Label = "start",
            Next = new BlockModel { Kind = BlockKind.Wait, Label = "wait", Number = 0.05 }
        }
    });

    var engine = new RuntimeEngine(project);
    engine.Tick(new MutableInputState(), 0.016);
    engine.Tick(new MutableInputState(), 0.016);
    engine.Tick(new MutableInputState(), 0.016);
    return Task.CompletedTask;
}

static Task TestListBlocks()
{
    var project = SampleProjectFactory.Create();
    project.Lists["items"] = [];
    var player = project.Scenes[0].Sprites[0];
    player.Scripts.Clear();
    player.Scripts.Add(new BlockScriptModel
    {
        Root = new BlockModel
        {
            Kind = BlockKind.EventGameStart,
            Label = "start",
            Next = new BlockModel { Kind = BlockKind.AddToList, Label = "hello", Text = "items" }
        }
    });

    var engine = new RuntimeEngine(project);
    engine.Tick(new MutableInputState(), 0.016);
    Assert(project.Lists["items"].Count == 1, "AddToList should append an item.");
    Assert(project.Lists["items"][0] == "hello", "List item should match.");
    return Task.CompletedTask;
}

static Task TestClickEvent()
{
    var project = SampleProjectFactory.Create();
    var player = project.Scenes[0].Sprites[0];
    player.Scripts.Clear();
    player.Scripts.Add(new BlockScriptModel
    {
        Root = new BlockModel
        {
            Kind = BlockKind.EventClick,
            Label = "click",
            Next = new BlockModel { Kind = BlockKind.SetX, Label = "set x", Number = 999 }
        }
    });

    var engine = new RuntimeEngine(project);
    var input = new MutableInputState { PointerX = player.X, PointerY = player.Y, IsClickActive = true };
    engine.Tick(input, 0.016);
    Assert(Math.Abs(player.X - 999) < 0.01, "Click event should run attached blocks.");
    return Task.CompletedTask;
}

static async Task TestEditorLayoutRoundTrip()
{
    var project = SampleProjectFactory.Create();
    project.EditorLayout.Nodes["abc"] = new BlockNodeLayout { X = 42, Y = 84 };
    var path = Path.Combine(Path.GetTempPath(), $"blockgame-layout-{Guid.NewGuid():N}.blockgame");
    await new ProjectSerializer().SaveAsync(project, path);
    var loaded = await new ProjectSerializer().LoadAsync(path);
    Assert(loaded.EditorLayout.Nodes["abc"].X == 42, "Editor layout X should round trip.");
    Assert(loaded.EditorLayout.Nodes["abc"].Y == 84, "Editor layout Y should round trip.");
}

static async Task TestAndroidExportBranding()
{
    var root = FindRepoRoot();
    var project = SampleProjectFactory.Create();
    project.Name = "My Game";
    project.Version = "2.3.4";
    project.ApplicationId = "com.example.mygame";
    var output = Path.Combine(Path.GetTempPath(), $"blockgame-brand-{Guid.NewGuid():N}");
    await new AndroidApkExportPipeline().ExportAsync(project, new ExportOptions
    {
        RepositoryRoot = root,
        OutputDirectory = output,
        BuildRelease = false
    });

    var csproj = await File.ReadAllTextAsync(Path.Combine(output, "BlockGame.AndroidPlayer.csproj"));
    Assert(csproj.Contains("com.example.mygame"), "Android export should apply ApplicationId.");
    Assert(csproj.Contains("2.3.4"), "Android export should apply version.");
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "BlockGameEngine.slnx")))
    {
        dir = dir.Parent;
    }

    return dir?.FullName ?? Directory.GetCurrentDirectory();
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
