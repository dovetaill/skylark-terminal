using System;

namespace SkylarkTerminal.Models;

public abstract class WorkspaceLayoutNode
{
    protected WorkspaceLayoutNode(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            throw new ArgumentException("Node id cannot be null or whitespace.", nameof(nodeId));
        }

        NodeId = nodeId;
    }

    public string NodeId { get; }
}
