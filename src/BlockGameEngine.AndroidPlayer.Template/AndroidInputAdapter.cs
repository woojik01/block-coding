using Android.Views;
using BlockGameEngine.Runtime;

namespace BlockGame.AndroidPlayer;

public sealed class AndroidInputAdapter : IInputState
{
    public bool IsClickActive { get; private set; }
    public double PointerX { get; private set; }
    public double PointerY { get; private set; }

    public bool IsKeyDown(string key) => false;

    public void HandleTouch(MotionEvent e)
    {
        PointerX = e.GetX();
        PointerY = e.GetY();
        IsClickActive = e.Action == MotionEventActions.Down || e.Action == MotionEventActions.Move;
        if (e.Action == MotionEventActions.Up || e.Action == MotionEventActions.Cancel)
        {
            IsClickActive = false;
        }
    }
}
