namespace BlockGameEngine.Runtime;

public sealed class RuntimeEngine
{
    private readonly BlockInterpreter interpreter = new();
    private readonly Dictionary<string, ScriptExecutionState> scriptStates = new();
    private bool started;

    public RuntimeEngine(ProjectModel project)
    {
        Project = project;
        CurrentScene = project.Scenes.FirstOrDefault(s => s.Name == project.StartSceneName)
            ?? project.Scenes.FirstOrDefault()
            ?? new SceneModel();
    }

    public ProjectModel Project { get; }
    public SceneModel CurrentScene { get; private set; }

    public void Start(IInputState input)
    {
        if (started)
        {
            return;
        }

        started = true;
        foreach (var sprite in CurrentScene.Sprites)
        {
            foreach (var script in sprite.Scripts.Where(s => s.Root.Kind == BlockKind.EventGameStart))
            {
                RunScript(sprite, script, input, forceStart: true);
            }
        }
    }

    public void Tick(IInputState input, double deltaSeconds)
    {
        Start(input);

        foreach (var sprite in CurrentScene.Sprites)
        {
            foreach (var script in sprite.Scripts)
            {
                var key = StateKey(sprite, script);
                var state = GetState(key);

                if (state.IsWaiting)
                {
                    RunScript(sprite, script, input, forceStart: false);
                    continue;
                }

                if (ShouldRunEvent(script.Root, sprite, input))
                {
                    RunScript(sprite, script, input, forceStart: true);
                }
            }
        }
    }

    private void RunScript(SpriteModel sprite, BlockScriptModel script, IInputState input, bool forceStart)
    {
        var key = StateKey(sprite, script);
        var state = GetState(key);
        var context = new ExecutionContext(Project, CurrentScene, sprite, input);

        if (forceStart && !state.IsWaiting)
        {
            state.ResumeAt = null;
            interpreter.Execute(script.Root.Next, context, state);
            return;
        }

        if (state.IsWaiting || state.ResumeAt is not null)
        {
            interpreter.Execute(state.ResumeAt ?? script.Root.Next, context, state);
        }
    }

    private ScriptExecutionState GetState(string key)
    {
        if (!scriptStates.TryGetValue(key, out var state))
        {
            state = new ScriptExecutionState();
            scriptStates[key] = state;
        }

        return state;
    }

    private static string StateKey(SpriteModel sprite, BlockScriptModel script)
        => $"{sprite.Id}:{script.Id}";

    private bool ShouldRunEvent(BlockModel root, SpriteModel sprite, IInputState input)
    {
        return root.Kind switch
        {
            BlockKind.EventKeyPressed => input.IsKeyDown(root.Text),
            BlockKind.EventClick => input.IsClickActive && sprite.Bounds.Intersects(new RectD(input.PointerX, input.PointerY, 1, 1)),
            BlockKind.EventCollision => CurrentScene.Sprites.Any(other => other.Id != sprite.Id && sprite.Bounds.Intersects(other.Bounds)),
            _ => false
        };
    }
}

public sealed class ExecutionContext
{
    public ExecutionContext(ProjectModel project, SceneModel scene, SpriteModel sprite, IInputState input)
    {
        Project = project;
        Scene = scene;
        Sprite = sprite;
        Input = input;
    }

    public ProjectModel Project { get; }
    public SceneModel Scene { get; }
    public SpriteModel Sprite { get; }
    public IInputState Input { get; }
}
