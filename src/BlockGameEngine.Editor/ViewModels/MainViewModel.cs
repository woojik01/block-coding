using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;
using BlockGameEngine.Export;
using BlockGameEngine.ProjectIO;
using BlockGameEngine.Runtime;
using Microsoft.Win32;

namespace BlockGameEngine.Editor.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ProjectSerializer serializer = new();
    private readonly MutableInputState input = new();
    private readonly DispatcherTimer timer;
    private readonly Stack<GraphSnapshot> undo = new();
    private readonly Stack<GraphSnapshot> redo = new();
    private ProjectModel project;
    private SceneModel currentScene;
    private SpriteModel? selectedSprite;
    private GraphBlockViewModel? selectedGraphBlock;
    private BlockScriptModel? selectedScript;
    private RuntimeEngine runtime;
    private double workspaceZoom = 1;
    private string status = "Ready. Drag blocks onto the canvas, connect sockets, press Run.";
    private string? currentProjectPath;
    private bool isExporting;
    private bool isRunning;

    public MainViewModel()
    {
        project = SampleProjectFactory.Create();
        currentScene = project.Scenes[0];
        runtime = new RuntimeEngine(project);
        Palette = new ObservableCollection<BlockPaletteItem>(BlockPaletteItem.CreateDefaults());
        GraphBlocks = new ObservableCollection<GraphBlockViewModel>();
        Scenes = new ObservableCollection<SceneModel>(project.Scenes);
        ScriptTabs =
        [
            new ScriptTabItem(BlockKind.EventGameStart, "Game Start"),
            new ScriptTabItem(BlockKind.EventKeyPressed, "Key Pressed"),
            new ScriptTabItem(BlockKind.EventClick, "Click"),
            new ScriptTabItem(BlockKind.EventCollision, "Collision")
        ];
        SelectedScriptTab = ScriptTabs[1];

        NewProjectCommand = new RelayCommand(NewProject, () => !isExporting);
        SaveProjectCommand = new RelayCommand(SaveProjectAsync, () => !isExporting);
        LoadProjectCommand = new RelayCommand(LoadProjectAsync, () => !isExporting);
        RunCommand = new RelayCommand(Run, () => !isExporting);
        ExportWindowsCommand = new RelayCommand(ExportWindowsAsync, () => !isExporting);
        ExportAndroidCommand = new RelayCommand(ExportAndroidAsync, () => !isExporting);
        CopyBlockCommand = new RelayCommand(CopyBlock, () => SelectedGraphBlock is not null && !SelectedGraphBlock.IsEventHat && !isExporting);
        DeleteBlockCommand = new RelayCommand(DeleteBlock, () => SelectedGraphBlock is not null && !SelectedGraphBlock.IsEventHat && !isExporting);
        UndoCommand = new RelayCommand(Undo, () => undo.Count > 0 && !isExporting);
        RedoCommand = new RelayCommand(Redo, () => redo.Count > 0 && !isExporting);
        ImportSpriteImageCommand = new RelayCommand(ImportSpriteImageAsync, () => SelectedSprite is not null && !isExporting);
        ImportIconCommand = new RelayCommand(ImportIconAsync, () => !isExporting);

        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        timer.Tick += (_, _) =>
        {
            runtime.Tick(input, 0.033);
            OnPropertyChanged(nameof(CurrentScene));
            OnPropertyChanged(nameof(PreviewSprites));
        };

        SelectedSprite = currentScene.Sprites.FirstOrDefault();
        LoadGraphFromSelectedScript();
    }

    public ObservableCollection<BlockPaletteItem> Palette { get; }
    public ObservableCollection<GraphBlockViewModel> GraphBlocks { get; }
    public ObservableCollection<SceneModel> Scenes { get; }
    public IReadOnlyList<ScriptTabItem> ScriptTabs { get; }

    public IEnumerable<SpriteModel> PreviewSprites => CurrentScene.Sprites;

    public string AssetsBaseDirectory =>
        string.IsNullOrWhiteSpace(currentProjectPath)
            ? AppContext.BaseDirectory
            : ProjectSerializer.GetProjectDirectory(currentProjectPath);

    public SceneModel CurrentScene
    {
        get => currentScene;
        set
        {
            if (SetProperty(ref currentScene, value))
            {
                OnPropertyChanged(nameof(PreviewSprites));
            }
        }
    }

    public SpriteModel? SelectedSprite
    {
        get => selectedSprite;
        set
        {
            if (SetProperty(ref selectedSprite, value))
            {
                LoadGraphFromSelectedScript();
            }
        }
    }

    private ScriptTabItem selectedScriptTab = null!;

    public ScriptTabItem SelectedScriptTab
    {
        get => selectedScriptTab;
        set
        {
            if (SetProperty(ref selectedScriptTab, value))
            {
                SyncGraphToSelectedScript();
                LoadGraphFromSelectedScript();
            }
        }
    }

    public GraphBlockViewModel? SelectedGraphBlock
    {
        get => selectedGraphBlock;
        set
        {
            if (SetProperty(ref selectedGraphBlock, value))
            {
                CopyBlockCommand.RaiseCanExecuteChanged();
                DeleteBlockCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public double WorkspaceZoom
    {
        get => workspaceZoom;
        set => SetProperty(ref workspaceZoom, value);
    }

    public string Status
    {
        get => status;
        set => SetProperty(ref status, value);
    }

    public string ProjectName
    {
        get => project.Name;
        set
        {
            project.Name = value;
            OnPropertyChanged();
        }
    }

    public string ProjectVersion
    {
        get => project.Version;
        set
        {
            project.Version = value;
            OnPropertyChanged();
        }
    }

    public string ApplicationId
    {
        get => project.ApplicationId;
        set
        {
            project.ApplicationId = value;
            OnPropertyChanged();
        }
    }

    public bool IsExporting
    {
        get => isExporting;
        private set
        {
            if (SetProperty(ref isExporting, value))
            {
                RefreshCommandStates();
            }
        }
    }

    public RelayCommand NewProjectCommand { get; }
    public RelayCommand SaveProjectCommand { get; }
    public RelayCommand LoadProjectCommand { get; }
    public RelayCommand RunCommand { get; }
    public RelayCommand ExportWindowsCommand { get; }
    public RelayCommand ExportAndroidCommand { get; }
    public RelayCommand CopyBlockCommand { get; }
    public RelayCommand DeleteBlockCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand ImportSpriteImageCommand { get; }
    public RelayCommand ImportIconCommand { get; }

    public void AddGraphBlock(GraphBlockViewModel block)
    {
        CaptureUndo();
        GraphBlocks.Add(block);
        SaveLayout(block);
        SyncGraphToSelectedScript();
        redo.Clear();
        RefreshUndoRedo();
        Status = $"Added block: {block.Label}";
    }

    public void OnGraphChanged()
    {
        foreach (var block in GraphBlocks)
        {
            SaveLayout(block);
        }

        SyncGraphToSelectedScript();
    }

    public void SetKey(string key, bool isDown) => input.SetKey(key, isDown);

    public void SetPointer(double x, double y, bool isClickActive)
    {
        input.PointerX = x;
        input.PointerY = y;
        input.IsClickActive = isClickActive;
    }

    public void ChangeScene(string sceneName)
    {
        var scene = project.Scenes.FirstOrDefault(s => s.Name == sceneName);
        if (scene is null)
        {
            return;
        }

        SyncGraphToSelectedScript();
        CurrentScene = scene;
        SelectedSprite = scene.Sprites.FirstOrDefault();
    }

    private void NewProject()
    {
        project = SampleProjectFactory.Create();
        currentProjectPath = null;
        Scenes.Clear();
        foreach (var scene in project.Scenes)
        {
            Scenes.Add(scene);
        }

        CurrentScene = project.Scenes[0];
        runtime = new RuntimeEngine(project);
        SelectedSprite = CurrentScene.Sprites.FirstOrDefault();
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(ProjectVersion));
        OnPropertyChanged(nameof(ApplicationId));
        OnPropertyChanged(nameof(AssetsBaseDirectory));
        Status = "New sample project created.";
    }

    private async Task SaveProjectAsync()
    {
        SyncGraphToSelectedScript();
        var dialog = new SaveFileDialog
        {
            Filter = "Block Game Project (*.blockgame)|*.blockgame",
            FileName = $"{project.Name}.blockgame"
        };
        if (dialog.ShowDialog() == true)
        {
            currentProjectPath = dialog.FileName;
            await serializer.SaveAsync(project, dialog.FileName);
            OnPropertyChanged(nameof(AssetsBaseDirectory));
            Status = $"Saved: {dialog.FileName}";
        }
    }

    private async Task LoadProjectAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Block Game Project (*.blockgame)|*.blockgame"
        };
        if (dialog.ShowDialog() == true)
        {
            project = await serializer.LoadAsync(dialog.FileName);
            currentProjectPath = dialog.FileName;
            Scenes.Clear();
            foreach (var scene in project.Scenes)
            {
                Scenes.Add(scene);
            }

            CurrentScene = project.Scenes.First();
            runtime = new RuntimeEngine(project);
            SelectedSprite = CurrentScene.Sprites.FirstOrDefault();
            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ProjectVersion));
            OnPropertyChanged(nameof(ApplicationId));
            OnPropertyChanged(nameof(AssetsBaseDirectory));
            Status = $"Loaded: {dialog.FileName}";
        }
    }

    private async Task ImportSpriteImageAsync()
    {
        if (SelectedSprite is null || string.IsNullOrWhiteSpace(currentProjectPath))
        {
            Status = "Save the project first before importing sprite images.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var relative = await ProjectSerializer.ImportAssetAsync(currentProjectPath, dialog.FileName);
        SelectedSprite.ImageAssetPath = relative;
        project.Assets.Add(new AssetModel
        {
            FileName = Path.GetFileName(dialog.FileName),
            RelativePath = relative
        });
        OnPropertyChanged(nameof(PreviewSprites));
        Status = $"Imported sprite image: {relative}";
    }

    private async Task ImportIconAsync()
    {
        if (string.IsNullOrWhiteSpace(currentProjectPath))
        {
            Status = "Save the project first before importing an icon.";
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Images|*.png;*.ico"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var relative = await ProjectSerializer.ImportAssetAsync(currentProjectPath, dialog.FileName);
        project.IconAssetPath = relative;
        Status = $"Imported project icon: {relative}";
    }

    private void Run()
    {
        SyncGraphToSelectedScript();
        runtime = new RuntimeEngine(project);
        if (!isRunning)
        {
            timer.Start();
            isRunning = true;
        }

        Status = "Running. Hold Right to move, click the preview for click events.";
    }

    private async Task ExportWindowsAsync()
    {
        SyncGraphToSelectedScript();
        IsExporting = true;
        Status = "Exporting Windows exe...";
        try
        {
            var root = FindRepositoryRoot();
            var output = Path.Combine(root, "outputs", "windows-export");
            var result = await new WindowsExeExportPipeline().ExportAsync(project, new ExportOptions
            {
                RepositoryRoot = root,
                OutputDirectory = output,
                ProjectFilePath = currentProjectPath
            });
            Status = result.Success
                ? $"SUCCESS: {result.Message} Output: {result.OutputPath}"
                : $"FAILED: {result.Message}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    private async Task ExportAndroidAsync()
    {
        SyncGraphToSelectedScript();
        IsExporting = true;
        Status = "Exporting Android project...";
        try
        {
            var root = FindRepositoryRoot();
            var output = Path.Combine(root, "outputs", "android-export");
            var result = await new AndroidApkExportPipeline().ExportAsync(project, new ExportOptions
            {
                RepositoryRoot = root,
                OutputDirectory = output,
                BuildRelease = false,
                ProjectFilePath = currentProjectPath
            });
            Status = result.Success
                ? $"SUCCESS: {result.Message} Output: {result.OutputPath}"
                : $"INFO: {result.Message} Output: {result.OutputPath}";
        }
        finally
        {
            IsExporting = false;
        }
    }

    private void CopyBlock()
    {
        if (SelectedGraphBlock is null || SelectedGraphBlock.IsEventHat)
        {
            return;
        }

        CaptureUndo();
        var copy = SelectedGraphBlock.Clone();
        copy.X += 24;
        copy.Y += 24;
        GraphBlocks.Add(copy);
        SaveLayout(copy);
        SyncGraphToSelectedScript();
        redo.Clear();
        RefreshUndoRedo();
    }

    private void DeleteBlock()
    {
        if (SelectedGraphBlock is null || SelectedGraphBlock.IsEventHat)
        {
            return;
        }

        CaptureUndo();
        var id = SelectedGraphBlock.Id;
        foreach (var block in GraphBlocks.Where(b => b.NextBlockId == id))
        {
            block.NextBlockId = null;
        }

        GraphBlocks.Remove(SelectedGraphBlock);
        project.EditorLayout.Nodes.Remove(id);
        SelectedGraphBlock = null;
        SyncGraphToSelectedScript();
        redo.Clear();
        RefreshUndoRedo();
    }

    private void Undo()
    {
        if (undo.TryPop(out var snapshot))
        {
            redo.Push(CaptureSnapshot());
            RestoreSnapshot(snapshot);
            RefreshUndoRedo();
        }
    }

    private void Redo()
    {
        if (redo.TryPop(out var snapshot))
        {
            undo.Push(CaptureSnapshot());
            RestoreSnapshot(snapshot);
            RefreshUndoRedo();
        }
    }

    private void LoadGraphFromSelectedScript()
    {
        GraphBlocks.Clear();
        SelectedGraphBlock = null;
        var sprite = SelectedSprite ?? CurrentScene.Sprites.FirstOrDefault();
        if (sprite is null)
        {
            return;
        }

        selectedScript = sprite.Scripts.FirstOrDefault(s => s.Root.Kind == SelectedScriptTab.EventKind);
        if (selectedScript is null)
        {
            selectedScript = CreateDefaultScript(sprite, SelectedScriptTab.EventKind);
            sprite.Scripts.Add(selectedScript);
        }

        var hat = GraphBlockViewModel.FromModel(
            selectedScript.Root,
            GraphBlockViewModel.ColorForKind(selectedScript.Root.Kind),
            isEventHat: true);
        ApplyLayout(hat);
        GraphBlocks.Add(hat);

        var blockMap = new Dictionary<string, GraphBlockViewModel> { [hat.Id] = hat };
        var current = selectedScript.Root.Next;
        var y = hat.Y + 70;

        while (current is not null)
        {
            var vm = GraphBlockViewModel.FromModel(current, GraphBlockViewModel.ColorForKind(current.Kind));
            ApplyLayout(vm);
            if (vm.X == 0 && vm.Y == 0)
            {
                vm.X = hat.X;
                vm.Y = y;
                y += 70;
            }

            if (current.Next is not null)
            {
                vm.NextBlockId = current.Next.Id;
            }

            blockMap[current.Id] = vm;
            GraphBlocks.Add(vm);
            current = current.Next;
        }

        if (selectedScript.Root.Next is not null && blockMap.TryGetValue(selectedScript.Root.Next.Id, out var firstBlock))
        {
            hat.NextBlockId = firstBlock.Id;
        }
    }

    private BlockScriptModel CreateDefaultScript(SpriteModel sprite, BlockKind kind)
    {
        var label = kind switch
        {
            BlockKind.EventGameStart => "when game starts",
            BlockKind.EventKeyPressed => "when Right key pressed",
            BlockKind.EventClick => "when clicked",
            BlockKind.EventCollision => "when touching another sprite",
            _ => "when event"
        };

        var text = kind == BlockKind.EventKeyPressed ? "Right" : "";
        return new BlockScriptModel
        {
            Root = new BlockModel { Kind = kind, Label = label, Text = text }
        };
    }

    private void SyncGraphToSelectedScript()
    {
        var sprite = SelectedSprite ?? CurrentScene.Sprites.FirstOrDefault();
        if (sprite is null || selectedScript is null)
        {
            return;
        }

        var hat = GraphBlocks.FirstOrDefault(b => b.IsEventHat);
        if (hat is null)
        {
            return;
        }

        selectedScript.Root.Kind = hat.Kind;
        selectedScript.Root.Label = hat.Label;
        selectedScript.Root.Text = hat.ToModel().Text;

        var lookup = GraphBlocks.Where(b => !b.IsEventHat).ToDictionary(b => b.Id);
        var visited = new HashSet<string>();
        BlockModel? first = null;
        BlockModel? previous = null;

        var currentVm = lookup.Values.FirstOrDefault(b => GraphBlocks.Any(h => h.IsEventHat && h.NextBlockId == b.Id))
            ?? lookup.Values.OrderBy(b => b.Y).FirstOrDefault();

        while (currentVm is not null && visited.Add(currentVm.Id))
        {
            var model = currentVm.ToModel();
            first ??= model;
            if (previous is not null)
            {
                previous.Next = model;
            }

            previous = model;
            currentVm = currentVm.NextBlockId is not null && lookup.TryGetValue(currentVm.NextBlockId, out var next)
                ? next
                : null;
        }

        selectedScript.Root.Next = first;
        foreach (var block in GraphBlocks)
        {
            SaveLayout(block);
        }
    }

    private void ApplyLayout(GraphBlockViewModel block)
    {
        if (project.EditorLayout.Nodes.TryGetValue(block.Id, out var layout))
        {
            block.X = layout.X;
            block.Y = layout.Y;
        }
    }

    private void SaveLayout(GraphBlockViewModel block)
    {
        project.EditorLayout.Nodes[block.Id] = new BlockNodeLayout { X = block.X, Y = block.Y };
    }

    private void RefreshUndoRedo()
    {
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
    }

    private void RefreshCommandStates()
    {
        NewProjectCommand.RaiseCanExecuteChanged();
        SaveProjectCommand.RaiseCanExecuteChanged();
        LoadProjectCommand.RaiseCanExecuteChanged();
        RunCommand.RaiseCanExecuteChanged();
        ExportWindowsCommand.RaiseCanExecuteChanged();
        ExportAndroidCommand.RaiseCanExecuteChanged();
        CopyBlockCommand.RaiseCanExecuteChanged();
        DeleteBlockCommand.RaiseCanExecuteChanged();
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        ImportSpriteImageCommand.RaiseCanExecuteChanged();
        ImportIconCommand.RaiseCanExecuteChanged();
    }

    private void CaptureUndo() => undo.Push(CaptureSnapshot());

    private GraphSnapshot CaptureSnapshot()
        => new(
            GraphBlocks.Select(b => b.Clone()).ToList(),
            SelectedScriptTab.EventKind,
            SelectedSprite?.Id);

    private void RestoreSnapshot(GraphSnapshot snapshot)
    {
        GraphBlocks.Clear();
        foreach (var block in snapshot.Blocks.Select(b => b.Clone()))
        {
            GraphBlocks.Add(block);
        }

        SelectedGraphBlock = null;
        SelectedScriptTab = ScriptTabs.First(t => t.EventKind == snapshot.ScriptKind);
        if (snapshot.SpriteId is not null)
        {
            SelectedSprite = CurrentScene.Sprites.FirstOrDefault(s => s.Id == snapshot.SpriteId);
        }

        SyncGraphToSelectedScript();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "BlockGameEngine.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }

    private sealed record GraphSnapshot(List<GraphBlockViewModel> Blocks, BlockKind ScriptKind, string? SpriteId);
}
