using Avalonia;
using SkylarkTerminal.Models;
using SkylarkTerminal.Views;

namespace SkylarkTerminal.Tests;

public class WorkspacePaneDropPolicyTests
{
    [Theory]
    [InlineData(10, 450, WorkspaceDropDirection.Left)]
    [InlineData(890, 450, WorkspaceDropDirection.Right)]
    [InlineData(450, 10, WorkspaceDropDirection.Top)]
    [InlineData(450, 890, WorkspaceDropDirection.Bottom)]
    public void ResolveDropDirection_WhenPointerHitsEdge_ReturnsExpectedDirection(
        double x,
        double y,
        WorkspaceDropDirection expected)
    {
        var direction = WorkspacePaneDropPolicy.ResolveDropDirection(
            new Point(x, y),
            width: 900,
            height: 900,
            minPaneSize: 340d);

        Assert.Equal(expected, direction);
    }

    [Fact]
    public void ResolveDropDirection_WhenPaneTooSmallForSplit_ReturnsNull()
    {
        var direction = WorkspacePaneDropPolicy.ResolveDropDirection(
            new Point(5, 300),
            width: 600,
            height: 600,
            minPaneSize: 340d);

        Assert.Null(direction);
    }

    [Fact]
    public void ResolveDropDirection_WhenPointerAwayFromEdges_ReturnsNull()
    {
        var direction = WorkspacePaneDropPolicy.ResolveDropDirection(
            new Point(450, 450),
            width: 900,
            height: 900,
            minPaneSize: 340d);

        Assert.Null(direction);
    }
}
