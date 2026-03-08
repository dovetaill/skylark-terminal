using System;

namespace SkylarkTerminal.Models;

public sealed class SplitNode : WorkspaceLayoutNode
{
    private double _ratio;

    public SplitNode(
        string nodeId,
        WorkspaceSplitOrientation orientation,
        double ratio,
        WorkspaceLayoutNode first,
        WorkspaceLayoutNode second)
        : base(nodeId)
    {
        Orientation = orientation;
        First = first ?? throw new ArgumentNullException(nameof(first));
        Second = second ?? throw new ArgumentNullException(nameof(second));
        Ratio = ratio;
    }

    public WorkspaceSplitOrientation Orientation { get; set; }

    public double Ratio
    {
        get => _ratio;
        set => _ratio = Math.Clamp(value, 0d, 1d);
    }

    public WorkspaceLayoutNode First { get; set; }

    public WorkspaceLayoutNode Second { get; set; }
}
