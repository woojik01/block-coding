using Android.App;
using Android.OS;
using Android.Views;

namespace BlockGame.AndroidPlayer;

[Activity(Label = "Exported Block Game", MainLauncher = true)]
public sealed class MainActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(new GameView(this));
    }
}

public sealed class GameView : View
{
    public GameView(Android.Content.Context context) : base(context) { }

    public override bool OnTouchEvent(MotionEvent? e)
    {
        // Touch events are mapped to the shared runtime input adapter in the full exporter.
        return true;
    }
}