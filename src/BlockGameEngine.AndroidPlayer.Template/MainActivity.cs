using Android.App;
using Android.OS;
using Android.Views;

namespace BlockGame.AndroidPlayer;

[Activity(Label = "Exported Block Game", MainLauncher = true, ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class MainActivity : Activity
{
    private GameView? gameView;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        gameView = new GameView(this);
        SetContentView(gameView);
    }

    protected override void OnResume()
    {
        base.OnResume();
        gameView?.StartLoop();
    }

    protected override void OnPause()
    {
        gameView?.StopLoop();
        base.OnPause();
    }
}
