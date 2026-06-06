namespace BlockGameEngine.Runtime;

public sealed class ProjectModel
{
    public string Name { get; set; } = "Untitled Game";
    public string Version { get; set; } = "1.0.0";
    public string ApplicationId { get; set; } = "com.blockgame.exported";
    public string? IconAssetPath { get; set; }
    public string StartSceneName { get; set; } = "Main";
    public Dictionary<string, double> Variables { get; set; } = new();
    public Dictionary<string, List<string>> Lists { get; set; } = new();
    public List<AssetModel> Assets { get; set; } = new();
    public List<SceneModel> Scenes { get; set; } = new();
    public EditorLayoutModel EditorLayout { get; set; } = new();
}

public sealed class AssetModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FileName { get; set; } = "";
    public string RelativePath { get; set; } = "";
}

public sealed class EditorLayoutModel
{
    public Dictionary<string, BlockNodeLayout> Nodes { get; set; } = new();
}

public sealed class BlockNodeLayout
{
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class SceneModel
{
    public string Name { get; set; } = "Main";
    public CameraModel Camera { get; set; } = new();
    public List<SpriteModel> Sprites { get; set; } = new();
}

public sealed class CameraModel
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Zoom { get; set; } = 1;
}

public sealed class SpriteModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Sprite";
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public double Size { get; set; } = 1;
    public double Width { get; set; } = 48;
    public double Height { get; set; } = 48;
    public string Fill { get; set; } = "#4F8CFF";
    public string? ImageAssetPath { get; set; }
    public Dictionary<string, double> Variables { get; set; } = new();
    public List<BlockScriptModel> Scripts { get; set; } = new();

    public RectD Bounds => new(X - Width * Size / 2, Y - Height * Size / 2, Width * Size, Height * Size);
}

public sealed class BlockScriptModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public BlockModel Root { get; set; } = new();
}

public sealed class BlockModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public BlockKind Kind { get; set; }
    public string Label { get; set; } = "";
    public double Number { get; set; }
    public string Text { get; set; } = "";
    public BlockModel? Next { get; set; }
    public List<BlockModel> Children { get; set; } = new();
}

public enum BlockKind
{
    EventGameStart,
    EventKeyPressed,
    EventClick,
    EventCollision,
    Move,
    Rotate,
    SetX,
    SetY,
    ChangeSize,
    Repeat,
    If,
    Wait,
    SetVariable,
    ChangeVariable,
    AddToList,
    RemoveFromList,
    ItemOfList,
    LengthOfList
}

public readonly record struct RectD(double X, double Y, double Width, double Height)
{
    public bool Intersects(RectD other)
    {
        return X < other.X + other.Width
            && X + Width > other.X
            && Y < other.Y + other.Height
            && Y + Height > other.Y;
    }
}
