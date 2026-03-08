using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class MainWindowWorkspaceDragLifecycleTests
{
    [Fact]
    public void DragLifecycle_SuccessPath_BeginUpdateDropAndDragCompletedNoop_EndsCleanly()
    {
        var vm = new MainWindowViewModel();

        var pane1 = Assert.Single(vm.WorkspacePanes);
        var splitSeedTab = vm.WorkspaceTabs[0];
        vm.BeginWorkspaceDragPreview(pane1.PaneId, splitSeedTab.Id, splitSeedTab);
        Assert.True(vm.CompleteWorkspaceDragDrop(pane1.PaneId, WorkspaceDropDirection.Right));

        var pane2 = vm.WorkspacePanes.Single(p => p.PaneId != pane1.PaneId);
        var dragged = Assert.Single(pane2.Tabs);

        vm.BeginWorkspaceDragPreview(pane2.PaneId, dragged.Id, dragged);
        vm.UpdateWorkspaceDragPreview(pane1.PaneId, dropDirection: null);

        Assert.True(vm.HasWorkspaceDragSession);
        Assert.True(vm.IsWorkspaceDragOverlayVisible);

        var accepted = vm.CompleteWorkspaceDragDrop(pane1.PaneId, dropDirection: null, targetIndex: 0);

        Assert.True(accepted);
        Assert.False(vm.HasWorkspaceDragSession);
        Assert.False(vm.IsWorkspaceDragOverlayVisible);
        Assert.Null(vm.WorkspaceDragHoverPaneId);
        Assert.Null(vm.WorkspaceDragHoverDirection);

        vm.CancelWorkspaceDragPreview();

        Assert.False(vm.HasWorkspaceDragSession);
        Assert.False(vm.IsWorkspaceDragOverlayVisible);
        Assert.Null(vm.WorkspaceDragHoverPaneId);
        Assert.Null(vm.WorkspaceDragHoverDirection);
    }

    [Fact]
    public void DragLifecycle_SplitRejectedThenCancel_EndsCleanlyWithoutMutatingTabs()
    {
        var vm = CreateVmAtPaneLimit();
        var sourcePane = vm.WorkspacePanes.Single(p => p.Tabs.Count > 0);
        var targetPane = vm.WorkspacePanes.First(p => p.PaneId != sourcePane.PaneId);
        var dragged = sourcePane.Tabs[0];

        var sourceTabsBefore = sourcePane.Tabs.ToArray();
        var targetTabsBefore = targetPane.Tabs.ToArray();

        vm.BeginWorkspaceDragPreview(sourcePane.PaneId, dragged.Id, dragged);
        vm.UpdateWorkspaceDragPreview(targetPane.PaneId, WorkspaceDropDirection.Left);

        var accepted = vm.CompleteWorkspaceDragDrop(targetPane.PaneId, WorkspaceDropDirection.Left);

        Assert.False(accepted);
        Assert.Equal(sourceTabsBefore, sourcePane.Tabs.ToArray());
        Assert.Equal(targetTabsBefore, targetPane.Tabs.ToArray());
        Assert.False(vm.HasWorkspaceDragSession);
        Assert.False(vm.IsWorkspaceDragOverlayVisible);
        Assert.Null(vm.WorkspaceDragHoverPaneId);
        Assert.Null(vm.WorkspaceDragHoverDirection);

        vm.CancelWorkspaceDragPreview();

        Assert.False(vm.HasWorkspaceDragSession);
        Assert.False(vm.IsWorkspaceDragOverlayVisible);
        Assert.Null(vm.WorkspaceDragHoverPaneId);
        Assert.Null(vm.WorkspaceDragHoverDirection);
    }

    [Fact]
    public void DragLifecycle_DragCompletedBeforeDrop_CompleteReturnsFalseAndStateRemainsClean()
    {
        var vm = new MainWindowViewModel();
        var pane = Assert.Single(vm.WorkspacePanes);
        var tab = vm.WorkspaceTabs[0];

        vm.BeginWorkspaceDragPreview(pane.PaneId, tab.Id, tab);
        vm.UpdateWorkspaceDragPreview(pane.PaneId, WorkspaceDropDirection.Bottom);

        vm.CancelWorkspaceDragPreview();
        var accepted = vm.CompleteWorkspaceDragDrop(pane.PaneId, WorkspaceDropDirection.Bottom);

        Assert.False(accepted);
        Assert.False(vm.HasWorkspaceDragSession);
        Assert.False(vm.IsWorkspaceDragOverlayVisible);
        Assert.Null(vm.WorkspaceDragHoverPaneId);
        Assert.Null(vm.WorkspaceDragHoverDirection);
    }

    private static MainWindowViewModel CreateVmAtPaneLimit()
    {
        var vm = new MainWindowViewModel();

        while (vm.WorkspacePanes.Count < vm.MaxWorkspacePaneCount)
        {
            var paneWithTab = vm.WorkspacePanes.Single(p => p.Tabs.Count > 0);
            var dragged = paneWithTab.Tabs[0];

            vm.BeginWorkspaceDragPreview(paneWithTab.PaneId, dragged.Id, dragged);
            var splitAccepted = vm.CompleteWorkspaceDragDrop(paneWithTab.PaneId, WorkspaceDropDirection.Right);
            Assert.True(splitAccepted);
        }

        Assert.Equal(vm.MaxWorkspacePaneCount, vm.WorkspacePanes.Count);
        Assert.Single(vm.WorkspacePanes, static pane => pane.Tabs.Count > 0);
        return vm;
    }
}
