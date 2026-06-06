using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using BlockGameEngine.Compiler;
using BlockGameEngine.Runtime;

namespace BlockGameEngine.WindowsPlayer;

public partial class MainWindow : Window
{
    private readonly MutableInputState input = new();
    private readonly DispatcherTimer timer;
    private readonly RuntimeEngine runtime;
    private readonly string assetsBase;

    public MainWindow()
    {
        InitializeComponent();

        var package = LoadPackage();
        runtime = new RuntimeEngine(package.Project);
        assetsBase = AppContext.BaseDirectory;
        StatusText.Text = $"{package.Project.Name} v{package.Project.Version} - hold Right to play, click to interact";

        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        timer.Tick += (_, _) =>
        {
            runtime.Tick(input, 0.033);
            RefreshSprites();
        };
        timer.Start();
        RefreshSprites();
    }

    private void RefreshSprites()
    {
        GameCanvas.Children.Clear();
        foreach (var sprite in runtime.CurrentScene.Sprites)
        {
            var element = SpriteVisualFactory.Create(sprite, assetsBase);
            Canvas.SetLeft(element, sprite.X - sprite.Width * sprite.Size / 2);
            Canvas.SetTop(element, sprite.Y - sprite.Height * sprite.Size / 2);
            GameCanvas.Children.Add(element);
        }
    }

    private static CompiledGamePackage LoadPackage()
    {
        var packagePath = Path.Combine(AppContext.BaseDirectory, "game.package.json");
        if (!File.Exists(packagePath))
        {
            return new CompiledGamePackage { Project = SampleProjectFactory.Create() };
        }

        var json = File.ReadAllText(packagePath);
        return JsonSerializer.Deserialize<CompiledGamePackage>(json)
            ?? new CompiledGamePackage { Project = SampleProjectFactory.Create() };
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        input.SetKey(e.Key.ToString(), true);
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        input.SetKey(e.Key.ToString(), false);
    }

    private void GameCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(GameCanvas);
        input.PointerX = pos.X;
        input.PointerY = pos.Y;
        input.IsClickActive = true;
    }

    private void GameCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(GameCanvas);
        input.PointerX = pos.X;
        input.PointerY = pos.Y;
        input.IsClickActive = false;
    }
}
