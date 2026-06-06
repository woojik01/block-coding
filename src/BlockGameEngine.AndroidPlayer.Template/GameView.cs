using System.Text.Json;
using Android.Content;
using Android.Graphics;
using Android.Views;
using BlockGameEngine.Compiler;
using BlockGameEngine.Runtime;

namespace BlockGame.AndroidPlayer;

public sealed class GameView : View
{
    private readonly AndroidInputAdapter input = new();
    private readonly RuntimeEngine runtime;
    private readonly CompiledGamePackage package;
    private readonly AndroidRenderer renderer = new();
    private bool running;
    private long lastTickMs;

    public GameView(Context context) : base(context)
    {
        package = PackageLoader.Load(context);
        runtime = new RuntimeEngine(package.Project);
        Focusable = true;
    }

    public void StartLoop()
    {
        running = true;
        lastTickMs = Java.Lang.JavaSystem.CurrentTimeMillis();
        Post(OnTick);
    }

    public void StopLoop()
    {
        running = false;
    }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null)
        {
            return base.OnTouchEvent(e);
        }

        input.HandleTouch(e);
        return true;
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        renderer.Draw(canvas, runtime.CurrentScene, Context.Assets);
    }

    private void OnTick()
    {
        if (!running)
        {
            return;
        }

        var now = Java.Lang.JavaSystem.CurrentTimeMillis();
        var delta = (now - lastTickMs) / 1000.0;
        lastTickMs = now;
        runtime.Tick(input, delta);
        Invalidate();
        PostDelayed(OnTick, 33);
    }
}

internal static class PackageLoader
{
    public static CompiledGamePackage Load(Context context)
    {
        try
        {
            using var stream = context.Assets!.Open("game.package.json");
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            return JsonSerializer.Deserialize<CompiledGamePackage>(json)
                ?? new CompiledGamePackage { Project = SampleProjectFactory.Create() };
        }
        catch
        {
            var path = Path.Combine(context.FilesDir!.AbsolutePath, "game.package.json");
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<CompiledGamePackage>(json)
                    ?? new CompiledGamePackage { Project = SampleProjectFactory.Create() };
            }
        }

        return new CompiledGamePackage { Project = SampleProjectFactory.Create() };
    }
}
