using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class MainWindowWorkspaceDragDropRollbackTests
{
    [Fact]
    public void CompleteWorkspaceDragDrop_CrossPaneSplitWhenSplitFails_RollsBackTabsSelectionAndLayout()
    {
        var layoutService = new ToggleableSplitWorkspaceLayoutService();
        var vm = CreateVm(layoutService);

        var pane1 = Assert.Single(vm.WorkspacePanes);
        vm.CreateWorkspaceTabCommand.Execute(null);

        var splitSeedTab = pane1.Tabs[0];
        vm.BeginWorkspaceDragPreview(pane1.PaneId, splitSeedTab.Id, splitSeedTab);
        Assert.True(vm.CompleteWorkspaceDragDrop(pane1.PaneId, WorkspaceDropDirection.Right));

        Assert.Equal(2, vm.WorkspacePanes.Count);
        var pane2 = vm.WorkspacePanes.Single(p => p.PaneId != pane1.PaneId);

        vm.SelectedWorkspacePane = pane2;
        vm.CreateWorkspaceTabCommand.Execute(null);
        Assert.Equal(2, pane2.Tabs.Count);

        var dragged = pane2.Tabs[0];
        var sourceTabsBefore = pane2.Tabs.ToArray();
        var targetTabsBefore = pane1.Tabs.ToArray();
        var layoutBefore = DescribeLayout(vm.WorkspaceLayoutRoot);

        layoutService.FailNextSplit = true;
        vm.BeginWorkspaceDragPreview(pane2.PaneId, dragged.Id, dragged);

        var accepted = vm.CompleteWorkspaceDragDrop(pane1.PaneId, WorkspaceDropDirection.Left);

        Assert.False(accepted);
        Assert.Equal(2, vm.WorkspacePanes.Count);
        Assert.Equal(layoutBefore, DescribeLayout(vm.WorkspaceLayoutRoot));
        Assert.Equal(sourceTabsBefore, pane2.Tabs.ToArray());
        Assert.Equal(targetTabsBefore, pane1.Tabs.ToArray());
        Assert.Same(pane2, vm.SelectedWorkspacePane);
        Assert.Same(dragged, vm.SelectedWorkspaceTab);
    }

    private static MainWindowViewModel CreateVm(IWorkspaceLayoutService layoutService)
    {
        var assetCatalog = new MockAssetCatalogService();
        var ssh = new MockSshConnectionService();
        var sftp = new MockSftpService();
        var dialog = new MockAppDialogService();
        var clipboard = new MockClipboardService();
        var registry = new SessionRegistryService(ssh);
        var drag = new DragSessionService();

        return new MainWindowViewModel(
            assetCatalog,
            ssh,
            sftp,
            dialog,
            clipboard,
            registry,
            layoutService,
            drag);
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

    private sealed class ToggleableSplitWorkspaceLayoutService : IWorkspaceLayoutService
    {
        private readonly WorkspaceLayoutService _inner = new();

        public bool FailNextSplit { get; set; }

        public WorkspaceLayoutNode Root => _inner.Root;

        public IReadOnlyCollection<string> PaneIds => _inner.PaneIds;

        public void InitializeRootPane(string paneId)
        {
            _inner.InitializeRootPane(paneId);
        }

        public bool MoveTab(string sourcePaneId, string targetPaneId, string tabId, int? index = null)
        {
            return _inner.MoveTab(sourcePaneId, targetPaneId, tabId, index);
        }

        public bool SplitAndMove(string sourcePaneId, string tabId, WorkspaceDropDirection dropDirection)
        {
            if (FailNextSplit)
            {
                FailNextSplit = false;
                return false;
            }

            return _inner.SplitAndMove(sourcePaneId, tabId, dropDirection);
        }

        public bool RecyclePaneIfEmpty(string paneId)
        {
            return _inner.RecyclePaneIfEmpty(paneId);
        }
    }
}
