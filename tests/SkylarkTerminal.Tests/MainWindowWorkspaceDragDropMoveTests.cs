using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class MainWindowWorkspaceDragDropMoveTests
{
    [Fact]
    public void CompleteWorkspaceDragDrop_SamePaneMove_WithNullIndex_AppendsToEnd()
    {
        var vm = CreateVmWithThreeTabsInSinglePane();
        var pane = Assert.Single(vm.WorkspacePanes);
        var dragged = vm.WorkspaceTabs[0];

        vm.BeginWorkspaceDragPreview(pane.PaneId, dragged.Id, dragged);

        var accepted = vm.CompleteWorkspaceDragDrop(pane.PaneId, dropDirection: null, targetIndex: null);

        Assert.True(accepted);
        Assert.Single(vm.WorkspacePanes);
        Assert.IsType<PaneNode>(vm.WorkspaceLayoutRoot);
        Assert.Same(dragged, vm.WorkspaceTabs[^1]);
        Assert.Same(pane, vm.SelectedWorkspacePane);
        Assert.Same(dragged, vm.SelectedWorkspaceTab);
    }

    [Fact]
    public void CompleteWorkspaceDragDrop_SamePaneMove_WithNegativeIndex_ClampsToStart()
    {
        var vm = CreateVmWithThreeTabsInSinglePane();
        var pane = Assert.Single(vm.WorkspacePanes);
        var dragged = vm.WorkspaceTabs[^1];

        vm.BeginWorkspaceDragPreview(pane.PaneId, dragged.Id, dragged);

        var accepted = vm.CompleteWorkspaceDragDrop(pane.PaneId, dropDirection: null, targetIndex: -3);

        Assert.True(accepted);
        Assert.Single(vm.WorkspacePanes);
        Assert.IsType<PaneNode>(vm.WorkspaceLayoutRoot);
        Assert.Same(dragged, vm.WorkspaceTabs[0]);
        Assert.Same(pane, vm.SelectedWorkspacePane);
        Assert.Same(dragged, vm.SelectedWorkspaceTab);
    }

    [Fact]
    public void CompleteWorkspaceDragDrop_SamePaneMove_WithOutOfRangeIndex_ClampsToEnd()
    {
        var vm = CreateVmWithThreeTabsInSinglePane();
        var pane = Assert.Single(vm.WorkspacePanes);
        var dragged = vm.WorkspaceTabs[0];

        vm.BeginWorkspaceDragPreview(pane.PaneId, dragged.Id, dragged);

        var accepted = vm.CompleteWorkspaceDragDrop(pane.PaneId, dropDirection: null, targetIndex: 99);

        Assert.True(accepted);
        Assert.Single(vm.WorkspacePanes);
        Assert.IsType<PaneNode>(vm.WorkspaceLayoutRoot);
        Assert.Same(dragged, vm.WorkspaceTabs[^1]);
        Assert.Same(pane, vm.SelectedWorkspacePane);
        Assert.Same(dragged, vm.SelectedWorkspaceTab);
    }

    [Fact]
    public void CompleteWorkspaceDragDrop_CrossPaneMove_MovesTabAndUpdatesSelection_WithoutNewSplit()
    {
        var vm = CreateVmWithThreeTabsInSinglePane();
        var pane1 = vm.WorkspacePanes.Single(p => p.PaneId == "pane-1");
        var splitSeedTab = pane1.Tabs[1];

        vm.BeginWorkspaceDragPreview(pane1.PaneId, splitSeedTab.Id, splitSeedTab);
        var splitAccepted = vm.CompleteWorkspaceDragDrop(pane1.PaneId, WorkspaceDropDirection.Right);
        Assert.True(splitAccepted);

        Assert.Equal(2, vm.WorkspacePanes.Count);
        var pane2 = vm.WorkspacePanes.Single(p => p.PaneId != "pane-1");
        Assert.IsType<SplitNode>(vm.WorkspaceLayoutRoot);

        vm.SelectedWorkspacePane = pane2;
        vm.CreateWorkspaceTabCommand.Execute(null);
        Assert.True(pane2.Tabs.Count >= 2);

        var draggedAcrossPane = pane2.Tabs[0];
        var paneIdsBefore = vm.WorkspacePanes.Select(p => p.PaneId).OrderBy(x => x).ToArray();
        var sourceCountBefore = pane2.Tabs.Count;
        var targetCountBefore = pane1.Tabs.Count;

        vm.BeginWorkspaceDragPreview(pane2.PaneId, draggedAcrossPane.Id, draggedAcrossPane);
        var moveAccepted = vm.CompleteWorkspaceDragDrop(pane1.PaneId, dropDirection: null, targetIndex: 0);

        Assert.True(moveAccepted);
        Assert.Equal(2, vm.WorkspacePanes.Count);
        Assert.IsType<SplitNode>(vm.WorkspaceLayoutRoot);
        Assert.Equal(paneIdsBefore, vm.WorkspacePanes.Select(p => p.PaneId).OrderBy(x => x).ToArray());

        Assert.Equal(sourceCountBefore - 1, pane2.Tabs.Count);
        Assert.Equal(targetCountBefore + 1, pane1.Tabs.Count);
        Assert.DoesNotContain(draggedAcrossPane, pane2.Tabs);
        Assert.Same(draggedAcrossPane, pane1.Tabs[0]);

        Assert.Same(pane1, vm.SelectedWorkspacePane);
        Assert.Same(draggedAcrossPane, vm.SelectedWorkspaceTab);
    }

    private static MainWindowViewModel CreateVmWithThreeTabsInSinglePane()
    {
        var vm = new MainWindowViewModel();
        vm.CreateWorkspaceTabCommand.Execute(null);
        vm.CreateWorkspaceTabCommand.Execute(null);

        Assert.Single(vm.WorkspacePanes);
        Assert.Equal(3, vm.WorkspaceTabs.Count);
        return vm;
    }
}
