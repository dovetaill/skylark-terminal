using SkylarkTerminal.Models;
using System.Collections.Generic;

namespace SkylarkTerminal.Services;

public interface IWorkspaceLayoutService
{
    WorkspaceLayoutNode Root { get; }

    IReadOnlyCollection<string> PaneIds { get; }

    void InitializeRootPane(string paneId);

    bool MoveTab(string sourcePaneId, string targetPaneId, string tabId, int? index = null);

    bool SplitAndMove(string sourcePaneId, string tabId, WorkspaceDropDirection dropDirection);

    bool RecyclePaneIfEmpty(string paneId);
}
