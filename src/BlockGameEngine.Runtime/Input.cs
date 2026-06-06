namespace BlockGameEngine.Runtime;

public interface IInputState
{
    bool IsKeyDown(string key);
    bool IsClickActive { get; }
    double PointerX { get; }
    double PointerY { get; }
}

public sealed class MutableInputState : IInputState
{
    private readonly HashSet<string> keys = new(StringComparer.OrdinalIgnoreCase);

    public bool IsClickActive { get; set; }
    public double PointerX { get; set; }
    public double PointerY { get; set; }

    public bool IsKeyDown(string key)
    {
        return keys.Contains(key);
    }

    public void SetKey(string key, bool isDown)
    {
        if (isDown)
        {
            keys.Add(key);
            return;
        }

        keys.Remove(key);
    }
}
