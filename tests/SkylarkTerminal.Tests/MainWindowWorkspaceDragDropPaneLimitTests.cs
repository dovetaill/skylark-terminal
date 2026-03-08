using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class MainWindowWorkspaceDragDropPaneLimitTests
{
    [Fact]
    public void CompleteWorkspaceDragDrop_WhenPaneCountHitsLimit_RejectsSplitAndSetsWarningMessage()
    {
        var vm = CreateVmAtPaneLimit();
        var sourcePane = vm.WorkspacePanes.Single(p => p.Tabs.Count > 0);
        var targetPane = vm.WorkspacePanes.First(p => p.PaneId != sourcePane.PaneId);
        var dragged = sourcePane.Tabs[0];

        var sourceTabsBefore = sourcePane.Tabs.ToArray();
        var targetTabsBefore = targetPane.Tabs.ToArray();
        var layoutBefore = DescribeLayout(vm.WorkspaceLayoutRoot);

        vm.BeginWorkspaceDragPreview(sourcePane.PaneId, dragged.Id, dragged);
        var accepted = vm.CompleteWorkspaceDragDrop(targetPane.PaneId, WorkspaceDropDirection.Left);

        Assert.False(accepted);
        Assert.Equal($"已达到最多 {vm.MaxWorkspacePaneCount} 个分屏，无法继续分屏", vm.LastAssetActionMessage);
        Assert.Equal(vm.MaxWorkspacePaneCount, vm.WorkspacePanes.Count);
        Assert.Equal(sourceTabsBefore, sourcePane.Tabs.ToArray());
        Assert.Equal(targetTabsBefore, targetPane.Tabs.ToArray());
        Assert.Equal(layoutBefore, DescribeLayout(vm.WorkspaceLayoutRoot));
        Assert.False(vm.HasWorkspaceDragSession);
        Assert.False(vm.IsWorkspaceDragOverlayVisible);
    }

    [Fact]
    public void CompleteWorkspaceDragDrop_WhenPaneCountHitsLimit_AllowsMoveWithoutSplit()
    {
        var vm = CreateVmAtPaneLimit();
        var sourcePane = vm.WorkspacePanes.Single(p => p.Tabs.Count > 0);
        vm.SelectedWorkspacePane = sourcePane;
        vm.CreateWorkspaceTabCommand.Execute(null);
        Assert.True(sourcePane.Tabs.Count >= 2);

        var targetPane = vm.WorkspacePanes.First(p => p.PaneId != sourcePane.PaneId);
        var dragged = sourcePane.Tabs[0];
        var sourceCountBefore = sourcePane.Tabs.Count;
        var targetCountBefore = targetPane.Tabs.Count;
        var paneCountBefore = vm.WorkspacePanes.Count;

        vm.BeginWorkspaceDragPreview(sourcePane.PaneId, dragged.Id, dragged);
        var accepted = vm.CompleteWorkspaceDragDrop(targetPane.PaneId, dropDirection: null, targetIndex: 0);

        Assert.True(accepted);
        Assert.Equal(paneCountBefore, vm.WorkspacePanes.Count);
        Assert.Equal(sourceCountBefore - 1, sourcePane.Tabs.Count);
        Assert.Equal(targetCountBefore + 1, targetPane.Tabs.Count);
        Assert.Same(dragged, targetPane.Tabs[0]);
        Assert.Same(targetPane, vm.SelectedWorkspacePane);
        Assert.Same(dragged, vm.SelectedWorkspaceTab);
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

    private static string DescribeLayout(WorkspaceLayoutNode node)
    {
        return node switch
        {
            PaneNode pane => $"P({pane.PaneId})",
            SplitNode split => $"S({split.Orientation},{DescribeLayout(split.First)},{DescribeLayout(split.Second)})",
            _ => node.NodeId,
        };
    }
}
