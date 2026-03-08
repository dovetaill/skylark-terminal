using System;

namespace SkylarkTerminal.Models;

public sealed class WorkspaceDragSession
{
    public WorkspaceDragSession(string sourcePaneId, string tabId, object? tabReference)
    {
        if (string.IsNullOrWhiteSpace(sourcePaneId))
        {
            throw new ArgumentException("Source pane id cannot be null or whitespace.", nameof(sourcePaneId));
        }

        if (string.IsNullOrWhiteSpace(tabId))
        {
            throw new ArgumentException("Tab id cannot be null or whitespace.", nameof(tabId));
        }

        SourcePaneId = sourcePaneId;
        TabId = tabId;
        TabReference = tabReference;
    }

    public string SourcePaneId { get; }

    public string TabId { get; }

    public object? TabReference { get; }

    public WorkspaceDragHoverTarget? HoverTarget { get; set; }
}
