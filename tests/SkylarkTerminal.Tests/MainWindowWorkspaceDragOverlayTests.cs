using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class MainWindowWorkspaceDragOverlayTests
{
    [Theory]
    [InlineData(WorkspaceDropDirection.Left)]
    [InlineData(WorkspaceDropDirection.Right)]
    [InlineData(WorkspaceDropDirection.Top)]
    [InlineData(WorkspaceDropDirection.Bottom)]
    public void UpdateWorkspaceDragPreview_WhenSessionActive_UpdatesHoverAndKeepsHotFlagsMutuallyExclusive(
        WorkspaceDropDirection direction)
    {
        var vm = new MainWindowViewModel();
        var pane = Assert.Single(vm.WorkspacePanes);
        var tab = vm.WorkspaceTabs[0];

        vm.BeginWorkspaceDragPreview(pane.PaneId, tab.Id, tab);
        vm.UpdateWorkspaceDragPreview(pane.PaneId, direction);

        Assert.True(vm.IsWorkspaceDragOverlayVisible);
        Assert.Equal(pane.PaneId, vm.WorkspaceDragHoverPaneId);
        Assert.Equal(direction, vm.WorkspaceDragHoverDirection);
        Assert.Single(GetHotDirections(vm));
        Assert.Contains(direction, GetHotDirections(vm));
    }

    [Fact]
    public void CommitWorkspaceDragPreview_AfterHoverUpdate_ClearsOverlayAndHotFlags()
    {
        var vm = new MainWindowViewModel();
        var pane = Assert.Single(vm.WorkspacePanes);
        var tab = vm.WorkspaceTabs[0];

        vm.BeginWorkspaceDragPreview(pane.PaneId, tab.Id, tab);
        vm.UpdateWorkspaceDragPreview(pane.PaneId, WorkspaceDropDirection.Right);

        var committed = vm.CommitWorkspaceDragPreview();

        Assert.NotNull(committed);
        Assert.False(vm.HasWorkspaceDragSession);
        Assert.False(vm.IsWorkspaceDragOverlayVisible);
        Assert.Null(vm.WorkspaceDragHoverPaneId);
        Assert.Null(vm.WorkspaceDragHoverDirection);
        Assert.Empty(GetHotDirections(vm));
    }

    [Fact]
    public void CancelWorkspaceDragPreview_AfterHoverUpdate_ClearsOverlayAndHotFlags()
    {
        var vm = new MainWindowViewModel();
        var pane = Assert.Single(vm.WorkspacePanes);
        var tab = vm.WorkspaceTabs[0];

        vm.BeginWorkspaceDragPreview(pane.PaneId, tab.Id, tab);
        vm.UpdateWorkspaceDragPreview(pane.PaneId, WorkspaceDropDirection.Top);

        vm.CancelWorkspaceDragPreview();

        Assert.False(vm.HasWorkspaceDragSession);
        Assert.False(vm.IsWorkspaceDragOverlayVisible);
        Assert.Null(vm.WorkspaceDragHoverPaneId);
        Assert.Null(vm.WorkspaceDragHoverDirection);
        Assert.Empty(GetHotDirections(vm));
    }

    [Fact]
    public void UpdateWorkspaceDragPreview_WhenSessionInactive_DoesNotChangeOverlayState()
    {
        var vm = new MainWindowViewModel();

        vm.UpdateWorkspaceDragPreview("pane-1", WorkspaceDropDirection.Left);

        Assert.False(vm.HasWorkspaceDragSession);
        Assert.False(vm.IsWorkspaceDragOverlayVisible);
        Assert.Null(vm.WorkspaceDragHoverPaneId);
        Assert.Null(vm.WorkspaceDragHoverDirection);
        Assert.Empty(GetHotDirections(vm));
    }

    private static WorkspaceDropDirection[] GetHotDirections(MainWindowViewModel vm)
    {
        var result = new List<WorkspaceDropDirection>();
        if (vm.IsWorkspaceDropSlotLeftHot)
        {
            result.Add(WorkspaceDropDirection.Left);
        }

        if (vm.IsWorkspaceDropSlotRightHot)
        {
            result.Add(WorkspaceDropDirection.Right);
        }

        if (vm.IsWorkspaceDropSlotTopHot)
        {
            result.Add(WorkspaceDropDirection.Top);
        }

        if (vm.IsWorkspaceDropSlotBottomHot)
        {
            result.Add(WorkspaceDropDirection.Bottom);
        }

        return result.ToArray();
    }
}
