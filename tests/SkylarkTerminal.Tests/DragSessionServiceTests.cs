using SkylarkTerminal.Models;
using SkylarkTerminal.Services;

namespace SkylarkTerminal.Tests;

public class DragSessionServiceTests
{
    [Fact]
    public void Start_SetsCurrentSessionAndMarksActive()
    {
        var service = new DragSessionService();
        var tabRef = new object();

        service.Start("pane-1", "tab-1", tabRef);

        Assert.True(service.IsActive);
        var session = Assert.IsType<WorkspaceDragSession>(service.Current);
        Assert.Equal("pane-1", session.SourcePaneId);
        Assert.Equal("tab-1", session.TabId);
        Assert.Same(tabRef, session.TabReference);
        Assert.Null(session.HoverTarget);
    }

    [Fact]
    public void UpdateHover_WhenActive_UpdatesHoverTarget()
    {
        var service = new DragSessionService();
        service.Start("pane-1", "tab-1");

        service.UpdateHover("pane-2", WorkspaceDropDirection.Bottom);

        var session = Assert.IsType<WorkspaceDragSession>(service.Current);
        var hover = Assert.IsType<WorkspaceDragHoverTarget>(session.HoverTarget);
        Assert.Equal("pane-2", hover.PaneId);
        Assert.Equal(WorkspaceDropDirection.Bottom, hover.DropDirection);
    }

    [Fact]
    public void Commit_ReturnsSessionAndClearsCurrent()
    {
        var service = new DragSessionService();
        service.Start("pane-1", "tab-1");

        var committed = service.Commit();

        Assert.NotNull(committed);
        Assert.False(service.IsActive);
        Assert.Null(service.Current);
    }

    [Fact]
    public void Cancel_ClearsCurrentSession()
    {
        var service = new DragSessionService();
        service.Start("pane-1", "tab-1");

        service.Cancel();

        Assert.False(service.IsActive);
        Assert.Null(service.Current);
    }

    [Fact]
    public void UpdateHover_WhenInactive_NoEffect()
    {
        var service = new DragSessionService();

        service.UpdateHover("pane-2", WorkspaceDropDirection.Left);

        Assert.False(service.IsActive);
        Assert.Null(service.Current);
    }
}

