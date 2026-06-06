namespace BlockGameEngine.Runtime;

public static class SampleProjectFactory
{
    public static ProjectModel Create()
    {
        var player = new SpriteModel
        {
            Name = "Player",
            X = 120,
            Y = 140,
            Fill = "#3E7BFA"
        };
        player.Scripts.Add(new BlockScriptModel
        {
            Root = new BlockModel
            {
                Kind = BlockKind.EventGameStart,
                Label = "when game starts",
                Next = new BlockModel
                {
                    Kind = BlockKind.SetVariable,
                    Label = "set score",
                    Text = "score",
                    Number = 0
                }
            }
        });
        player.Scripts.Add(new BlockScriptModel
        {
            Root = new BlockModel
            {
                Kind = BlockKind.EventKeyPressed,
                Label = "when Right key pressed",
                Text = "Right",
                Next = new BlockModel
                {
                    Kind = BlockKind.Move,
                    Label = "move 8 steps",
                    Number = 8
                }
            }
        });

        var coin = new SpriteModel
        {
            Name = "Goal",
            X = 320,
            Y = 140,
            Fill = "#FFB703"
        };
        coin.Scripts.Add(new BlockScriptModel
        {
            Root = new BlockModel
            {
                Kind = BlockKind.EventCollision,
                Label = "when touching another sprite",
                Next = new BlockModel
                {
                    Kind = BlockKind.ChangeVariable,
                    Label = "change score",
                    Text = "score",
                    Number = 1
                }
            }
        });

        return new ProjectModel
        {
            Name = "MVP Block Game",
            StartSceneName = "Main",
            Variables = new Dictionary<string, double> { ["score"] = 0 },
            Scenes =
            [
                new SceneModel
                {
                    Name = "Main",
                    Sprites = [player, coin]
                }
            ]
        };
    }
}
