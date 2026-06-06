namespace BlockGameEngine.Runtime;

public sealed class BlockInterpreter
{
    private const int MaxRepeatCount = 1000;
    private const double TicksPerSecond = 60;

    public bool Execute(BlockModel? block, ExecutionContext context, ScriptExecutionState? state = null)
    {
        if (state is not null && state.IsWaiting)
        {
            state.WaitTicksRemaining--;
            if (state.IsWaiting)
            {
                return false;
            }

            block = state.ResumeAt;
            state.ResumeAt = null;
        }

        var current = block;
        while (current is not null)
        {
            var paused = ExecuteSingle(current, context, state);
            if (paused)
            {
                if (state is not null)
                {
                    state.ResumeAt = current.Next;
                }

                return false;
            }

            current = current.Next;
        }

        state?.ResumeAt = null;
        return true;
    }

    private bool ExecuteSingle(BlockModel block, ExecutionContext context, ScriptExecutionState? state)
    {
        switch (block.Kind)
        {
            case BlockKind.Move:
                var radians = context.Sprite.Rotation * Math.PI / 180;
                context.Sprite.X += Math.Cos(radians) * block.Number;
                context.Sprite.Y += Math.Sin(radians) * block.Number;
                break;
            case BlockKind.Rotate:
                context.Sprite.Rotation += block.Number;
                break;
            case BlockKind.SetX:
                context.Sprite.X = block.Number;
                break;
            case BlockKind.SetY:
                context.Sprite.Y = block.Number;
                break;
            case BlockKind.ChangeSize:
                context.Sprite.Size = Math.Max(0.1, context.Sprite.Size + block.Number);
                break;
            case BlockKind.Repeat:
                var count = Math.Clamp((int)block.Number, 0, MaxRepeatCount);
                for (var i = 0; i < count; i++)
                {
                    foreach (var child in block.Children)
                    {
                        Execute(child, context);
                    }
                }
                break;
            case BlockKind.If:
                if (ReadValue(block.Text, context) != 0)
                {
                    foreach (var child in block.Children)
                    {
                        Execute(child, context);
                    }
                }
                break;
            case BlockKind.SetVariable:
                context.Project.Variables[block.Text] = block.Number;
                break;
            case BlockKind.ChangeVariable:
                context.Project.Variables[block.Text] = ReadValue(block.Text, context) + block.Number;
                break;
            case BlockKind.Wait:
                if (state is not null)
                {
                    state.WaitTicksRemaining = Math.Max(1, (int)(block.Number * TicksPerSecond));
                    return true;
                }
                break;
            case BlockKind.AddToList:
                GetOrCreateList(context.Project, block.Text).Add(block.Label);
                break;
            case BlockKind.RemoveFromList:
                GetOrCreateList(context.Project, block.Text).Remove(block.Label);
                break;
            case BlockKind.ItemOfList:
                var list = GetOrCreateList(context.Project, block.Text);
                var index = Math.Clamp((int)block.Number - 1, 0, Math.Max(0, list.Count - 1));
                if (list.Count > 0)
                {
                    context.Project.Variables[$"__item_{block.Id}"] = list[index].GetHashCode() % 1000;
                }
                break;
            case BlockKind.LengthOfList:
                context.Project.Variables[block.Text] = GetOrCreateList(context.Project, block.Text).Count;
                break;
        }

        return false;
    }

    private static List<string> GetOrCreateList(ProjectModel project, string name)
    {
        if (!project.Lists.TryGetValue(name, out var list))
        {
            list = [];
            project.Lists[name] = list;
        }

        return list;
    }

    private static double ReadValue(string name, ExecutionContext context)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return 0;
        }

        if (context.Sprite.Variables.TryGetValue(name, out var spriteValue))
        {
            return spriteValue;
        }

        return context.Project.Variables.TryGetValue(name, out var projectValue) ? projectValue : 0;
    }
}
