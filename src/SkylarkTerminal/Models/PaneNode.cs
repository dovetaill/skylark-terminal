using System;

namespace SkylarkTerminal.Models;

public sealed class PaneNode : WorkspaceLayoutNode
{
    public PaneNode(string paneId)
        : base($"pane:{paneId}")
    {
        if (string.IsNullOrWhiteSpace(paneId))
        {
            throw new ArgumentException("Pane id cannot be null or whitespace.", nameof(paneId));
        }

        PaneId = paneId;
    }

    public string PaneId { get; }
}
