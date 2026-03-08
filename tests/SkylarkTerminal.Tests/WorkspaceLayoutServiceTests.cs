using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using System.Reflection;

namespace SkylarkTerminal.Tests;

public class WorkspaceLayoutServiceTests
{
    [Fact]
    public void Constructor_InitializesSingleRootPane()
    {
        var service = new WorkspaceLayoutService();

        var root = Assert.IsType<PaneNode>(service.Root);
        Assert.Equal("pane-1", root.PaneId);
        Assert.Contains("pane-1", service.PaneIds);
        Assert.Single(service.PaneIds);
    }

    [Fact]
    public void SplitAndMove_CreatesNewPaneAndMovesTab()
    {
        var service = new WorkspaceLayoutService();
        service.MoveTab("pane-1", "pane-1", "tab-1");

        var splitOk = service.SplitAndMove("pane-1", "tab-1", WorkspaceDropDirection.Right);

        Assert.True(splitOk);
        Assert.Equal(2, service.PaneIds.Count);
        var root = Assert.IsType<SplitNode>(service.Root);
        Assert.Equal(WorkspaceSplitOrientation.Horizontal, root.Orientation);

        var tabsByPane = GetTabsByPane(service);
        Assert.Equal(Array.Empty<string>(), tabsByPane["pane-1"]);
        Assert.Equal(["tab-1"], tabsByPane["pane-2"]);
    }

    [Fact]
    public void MoveTab_CrossPane_MovesTrackedTab()
    {
        var service = new WorkspaceLayoutService();
        service.MoveTab("pane-1", "pane-1", "tab-1");
        service.SplitAndMove("pane-1", "tab-1", WorkspaceDropDirection.Right);
        service.MoveTab("pane-1", "pane-1", "tab-2");

        var moved = service.MoveTab("pane-1", "pane-2", "tab-2", 0);

        Assert.True(moved);
        var tabsByPane = GetTabsByPane(service);
        Assert.Equal(Array.Empty<string>(), tabsByPane["pane-1"]);
        Assert.Equal(["tab-2", "tab-1"], tabsByPane["pane-2"]);
    }

    [Fact]
    public void RecyclePaneIfEmpty_RemovesEmptyPaneAndCompressesTree()
    {
        var service = new WorkspaceLayoutService();
        service.MoveTab("pane-1", "pane-1", "tab-1");
        service.SplitAndMove("pane-1", "tab-1", WorkspaceDropDirection.Right);

        var recycled = service.RecyclePaneIfEmpty("pane-1");

        Assert.True(recycled);
        Assert.Single(service.PaneIds);
        Assert.Contains("pane-2", service.PaneIds);
        var root = Assert.IsType<PaneNode>(service.Root);
        Assert.Equal("pane-2", root.PaneId);
    }

    [Fact]
    public void RecyclePaneIfEmpty_WhenSinglePane_ReturnsFalse()
    {
        var service = new WorkspaceLayoutService();

        var recycled = service.RecyclePaneIfEmpty("pane-1");

        Assert.False(recycled);
        Assert.Single(service.PaneIds);
    }

    [Fact]
    public void SplitAndMove_UnknownPane_ReturnsFalse()
    {
        var service = new WorkspaceLayoutService();

        var splitOk = service.SplitAndMove("missing-pane", "tab-1", WorkspaceDropDirection.Left);

        Assert.False(splitOk);
        Assert.Single(service.PaneIds);
    }

    private static Dictionary<string, string[]> GetTabsByPane(WorkspaceLayoutService service)
    {
        var field = typeof(WorkspaceLayoutService).GetField(
            "_tabsByPane",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var value = field!.GetValue(service);
        var map = Assert.IsType<Dictionary<string, List<string>>>(value);
        return map.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }
}

