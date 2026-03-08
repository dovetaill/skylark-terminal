using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class MainWindowWorkspaceDragDropSplitTests
{
    [Fact]
    public void CompleteWorkspaceDragDrop_SplitLeft_CreatesHorizontalSplitWithCreatedPaneOnFirst()
    {
        var vm = new MainWindowViewModel();
        var sourcePane = Assert.Single(vm.WorkspacePanes);
        var dragged = vm.WorkspaceTabs[0];

        vm.BeginWorkspaceDragPreview(sourcePane.PaneId, dragged.Id, dragged);

        var accepted = vm.CompleteWorkspaceDragDrop(sourcePane.PaneId, WorkspaceDropDirection.Left);

        Assert.True(accepted);
        AssertSplitOutcome(
            vm,
            sourcePane,
            dragged,
            expectedOrientation: WorkspaceSplitOrientation.Horizontal,
            expectCreatedPaneOnFirst: true);
    }

    [Fact]
    public void CompleteWorkspaceDragDrop_SplitRight_CreatesHorizontalSplitWithCreatedPaneOnSecond()
    {
        var vm = new MainWindowViewModel();
        var sourcePane = Assert.Single(vm.WorkspacePanes);
        var dragged = vm.WorkspaceTabs[0];

        vm.BeginWorkspaceDragPreview(sourcePane.PaneId, dragged.Id, dragged);

        var accepted = vm.CompleteWorkspaceDragDrop(sourcePane.PaneId, WorkspaceDropDirection.Right);

        Assert.True(accepted);
        AssertSplitOutcome(
            vm,
            sourcePane,
            dragged,
            expectedOrientation: WorkspaceSplitOrientation.Horizontal,
            expectCreatedPaneOnFirst: false);
    }

    [Fact]
    public void CompleteWorkspaceDragDrop_SplitTop_CreatesVerticalSplitWithCreatedPaneOnFirst()
    {
        var vm = new MainWindowViewModel();
        var sourcePane = Assert.Single(vm.WorkspacePanes);
        var dragged = vm.WorkspaceTabs[0];

        vm.BeginWorkspaceDragPreview(sourcePane.PaneId, dragged.Id, dragged);

        var accepted = vm.CompleteWorkspaceDragDrop(sourcePane.PaneId, WorkspaceDropDirection.Top);

        Assert.True(accepted);
        AssertSplitOutcome(
            vm,
            sourcePane,
            dragged,
            expectedOrientation: WorkspaceSplitOrientation.Vertical,
            expectCreatedPaneOnFirst: true);
    }

    [Fact]
    public void CompleteWorkspaceDragDrop_SplitBottom_CreatesVerticalSplitWithCreatedPaneOnSecond()
    {
        var vm = new MainWindowViewModel();
        var sourcePane = Assert.Single(vm.WorkspacePanes);
        var dragged = vm.WorkspaceTabs[0];

        vm.BeginWorkspaceDragPreview(sourcePane.PaneId, dragged.Id, dragged);

        var accepted = vm.CompleteWorkspaceDragDrop(sourcePane.PaneId, WorkspaceDropDirection.Bottom);

        Assert.True(accepted);
        AssertSplitOutcome(
            vm,
            sourcePane,
            dragged,
            expectedOrientation: WorkspaceSplitOrientation.Vertical,
            expectCreatedPaneOnFirst: false);
    }

    private static void AssertSplitOutcome(
        MainWindowViewModel vm,
        WorkspacePaneViewModel sourcePane,
        WorkspaceTabItemViewModel dragged,
        WorkspaceSplitOrientation expectedOrientation,
        bool expectCreatedPaneOnFirst)
    {
        Assert.Equal(2, vm.WorkspacePanes.Count);

        var root = Assert.IsType<SplitNode>(vm.WorkspaceLayoutRoot);
        Assert.Equal(expectedOrientation, root.Orientation);

        var first = Assert.IsType<PaneNode>(root.First);
        var second = Assert.IsType<PaneNode>(root.Second);

        string createdPaneId;
        if (expectCreatedPaneOnFirst)
        {
            Assert.NotEqual(sourcePane.PaneId, first.PaneId);
            Assert.Equal(sourcePane.PaneId, second.PaneId);
            createdPaneId = first.PaneId;
        }
        else
        {
            Assert.Equal(sourcePane.PaneId, first.PaneId);
            Assert.NotEqual(sourcePane.PaneId, second.PaneId);
            createdPaneId = second.PaneId;
        }

        var createdPane = vm.WorkspacePanes.Single(p => p.PaneId == createdPaneId);

        Assert.DoesNotContain(dragged, sourcePane.Tabs);
        Assert.Contains(dragged, createdPane.Tabs);
        Assert.Same(createdPane, vm.SelectedWorkspacePane);
        Assert.Same(dragged, vm.SelectedWorkspaceTab);
    }
}
