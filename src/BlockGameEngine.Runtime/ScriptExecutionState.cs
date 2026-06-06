namespace BlockGameEngine.Runtime;

public sealed class ScriptExecutionState
{
    public BlockModel? ResumeAt { get; set; }
    public int WaitTicksRemaining { get; set; }

    public bool IsWaiting => WaitTicksRemaining > 0;
}
