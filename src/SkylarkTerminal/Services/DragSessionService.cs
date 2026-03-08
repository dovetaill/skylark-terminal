using SkylarkTerminal.Models;

namespace SkylarkTerminal.Services;

public sealed class DragSessionService : IDragSessionService
{
    public bool IsActive => Current is not null;

    public WorkspaceDragSession? Current { get; private set; }

    public void Start(string sourcePaneId, string tabId, object? tabReference = null)
    {
        Current = new WorkspaceDragSession(sourcePaneId, tabId, tabReference);
    }

    public void UpdateHover(string targetPaneId, WorkspaceDropDirection? dropDirection)
    {
        if (Current is null)
        {
            return;
        }

        Current.HoverTarget = new WorkspaceDragHoverTarget(targetPaneId, dropDirection);
    }

    public WorkspaceDragSession? Commit()
    {
        var session = Current;
        Current = null;
        return session;
    }

    public void Cancel()
    {
        Current = null;
    }
}
