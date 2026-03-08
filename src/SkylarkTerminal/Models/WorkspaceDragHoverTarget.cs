namespace SkylarkTerminal.Models;

public readonly record struct WorkspaceDragHoverTarget(
    string PaneId,
    WorkspaceDropDirection? DropDirection);
