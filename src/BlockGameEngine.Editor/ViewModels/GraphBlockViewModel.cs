using BlockGameEngine.Runtime;

namespace BlockGameEngine.Editor.ViewModels;

public sealed class GraphBlockViewModel : ObservableObject
{
    private double x;
    private double y;
    private string? nextBlockId;

    public GraphBlockViewModel(string id, BlockKind kind, string label, string color, bool isEventHat = false)
    {
        Id = id;
        Kind = kind;
        Label = label;
        Color = color;
        IsEventHat = isEventHat;
    }

    public string Id { get; }
    public BlockKind Kind { get; }
    public string Label { get; }
    public string Color { get; }
    public bool IsEventHat { get; }

    public double X
    {
        get => x;
        set => SetProperty(ref x, value);
    }

    public double Y
    {
        get => y;
        set => SetProperty(ref y, value);
    }

    public string? NextBlockId
    {
        get => nextBlockId;
        set => SetProperty(ref nextBlockId, value);
    }

    public GraphBlockViewModel Clone()
    {
        return new GraphBlockViewModel(Id, Kind, Label, Color, IsEventHat)
        {
            X = X,
            Y = Y,
            NextBlockId = NextBlockId
        };
    }

    public BlockModel ToModel()
    {
        return Kind switch
        {
            BlockKind.Move => new BlockModel { Id = Id, Kind = Kind, Label = Label, Number = 8 },
            BlockKind.Rotate => new BlockModel { Id = Id, Kind = Kind, Label = Label, Number = 15 },
            BlockKind.SetX => new BlockModel { Id = Id, Kind = Kind, Label = Label, Number = 100 },
            BlockKind.SetY => new BlockModel { Id = Id, Kind = Kind, Label = Label, Number = 100 },
            BlockKind.Repeat => new BlockModel
            {
                Id = Id,
                Kind = Kind,
                Label = Label,
                Number = 3,
                Children = [new BlockModel { Kind = BlockKind.Move, Label = "move 8 steps", Number = 8 }]
            },
            BlockKind.If => new BlockModel
            {
                Id = Id,
                Kind = Kind,
                Label = Label,
                Text = "score",
                Children = [new BlockModel { Kind = BlockKind.Rotate, Label = "turn 15 degrees", Number = 15 }]
            },
            BlockKind.SetVariable => new BlockModel { Id = Id, Kind = Kind, Label = Label, Text = "score", Number = 0 },
            BlockKind.ChangeVariable => new BlockModel { Id = Id, Kind = Kind, Label = Label, Text = "score", Number = 1 },
            BlockKind.Wait => new BlockModel { Id = Id, Kind = Kind, Label = Label, Number = 1 },
            BlockKind.AddToList => new BlockModel { Id = Id, Kind = Kind, Label = "item", Text = "items", Number = 0 },
            BlockKind.RemoveFromList => new BlockModel { Id = Id, Kind = Kind, Label = "item", Text = "items", Number = 0 },
            BlockKind.LengthOfList => new BlockModel { Id = Id, Kind = Kind, Label = Label, Text = "items", Number = 0 },
            _ => new BlockModel { Id = Id, Kind = Kind, Label = Label, Text = Kind == BlockKind.EventKeyPressed ? "Right" : "" }
        };
    }

    public static GraphBlockViewModel FromModel(BlockModel model, string color, bool isEventHat = false)
    {
        var vm = new GraphBlockViewModel(model.Id, model.Kind, model.Label, color, isEventHat);
        return vm;
    }

    public static string ColorForKind(BlockKind kind)
        => kind is BlockKind.SetVariable or BlockKind.ChangeVariable or BlockKind.AddToList
            or BlockKind.RemoveFromList or BlockKind.LengthOfList ? "#F59E0B"
            : kind is BlockKind.Repeat or BlockKind.If or BlockKind.Wait ? "#E879F9"
            : kind is BlockKind.EventGameStart or BlockKind.EventKeyPressed or BlockKind.EventClick or BlockKind.EventCollision ? "#F97316"
            : "#2563EB";
}

public sealed record BlockPaletteItem(BlockKind Kind, string Label, string Color)
{
    public static IEnumerable<BlockPaletteItem> CreateDefaults()
    {
        yield return new(BlockKind.Move, "move 8 steps", "#2563EB");
        yield return new(BlockKind.Rotate, "turn 15 degrees", "#2563EB");
        yield return new(BlockKind.SetX, "set x", "#2563EB");
        yield return new(BlockKind.SetY, "set y", "#2563EB");
        yield return new(BlockKind.Repeat, "repeat 3", "#E879F9");
        yield return new(BlockKind.If, "if score", "#E879F9");
        yield return new(BlockKind.Wait, "wait 1 sec", "#E879F9");
        yield return new(BlockKind.SetVariable, "set variable", "#F59E0B");
        yield return new(BlockKind.ChangeVariable, "change variable", "#F59E0B");
        yield return new(BlockKind.AddToList, "add to list", "#F59E0B");
        yield return new(BlockKind.RemoveFromList, "remove from list", "#F59E0B");
        yield return new(BlockKind.LengthOfList, "length of list", "#F59E0B");
    }
}

public sealed record ScriptTabItem(BlockKind EventKind, string Label);
