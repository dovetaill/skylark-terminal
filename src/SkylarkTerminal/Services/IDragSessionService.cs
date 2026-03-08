using SkylarkTerminal.Models;

namespace SkylarkTerminal.Services;

public interface IDragSessionService
{
    bool IsActive { get; }

    WorkspaceDragSession? Current { get; }

    void Start(string sourcePaneId, string tabId, object? tabReference = null);

    void UpdateHover(string targetPaneId, WorkspaceDropDirection? dropDirection);

    WorkspaceDragSession? Commit();

    void Cancel();
}
