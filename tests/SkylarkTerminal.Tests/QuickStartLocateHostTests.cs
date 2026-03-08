using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Tests;

public class QuickStartLocateHostTests
{
    [Fact]
    public void LocateHostFromQuickStart_SelectedTabHasConnection_PrefersCurrentTabTarget()
    {
        var vm = new MainWindowViewModel();
        var firstRecent = vm.FilteredQuickStartRecentConnections.First();
        var selectedRecent = vm.FilteredQuickStartRecentConnections.Skip(1).First();

        vm.OpenQuickStartConnectionCommand.Execute(selectedRecent);
        Assert.NotNull(vm.SelectedWorkspaceTab?.ConnectionConfig);

        vm.LocateHostFromQuickStartCommand.Execute(null);

        var pending = Assert.IsType<ConnectionNode>(vm.PendingQuickStartLocateTarget);
        Assert.Equal(selectedRecent.DisplayName, pending.Name);
        Assert.Equal(selectedRecent.Host, pending.Host, ignoreCase: true);
        Assert.Equal(selectedRecent.Username, pending.User, ignoreCase: true);
        Assert.Equal(selectedRecent.Port, pending.Port);
        Assert.Equal(AssetsPaneKind.Hosts, vm.SelectedAssetsPane);
        Assert.Contains(selectedRecent.DisplayName, vm.LastAssetActionMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(firstRecent.DisplayName, vm.LastAssetActionMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void LocateHostFromQuickStart_NoTabConnection_UsesFirstFilteredRecent()
    {
        var vm = new MainWindowViewModel();
        var expected = vm.FilteredQuickStartRecentConnections.First();
        vm.IsAssetsPanelVisible = false;
        vm.SelectedAssetsPane = AssetsPaneKind.Sftp;

        vm.LocateHostFromQuickStartCommand.Execute(null);

        var pending = Assert.IsType<ConnectionNode>(vm.PendingQuickStartLocateTarget);
        Assert.Equal(expected.DisplayName, pending.Name);
        Assert.Equal(expected.Host, pending.Host, ignoreCase: true);
        Assert.Equal(expected.Username, pending.User, ignoreCase: true);
        Assert.Equal(expected.Port, pending.Port);
        Assert.True(vm.IsAssetsPanelVisible);
        Assert.Equal(AssetsPaneKind.Hosts, vm.SelectedAssetsPane);
        Assert.Contains(expected.DisplayName, vm.LastAssetActionMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void LocateHostFromQuickStart_NoTabConnectionAndNoRecent_OpensHostsAndReportsNoTarget()
    {
        var vm = new MainWindowViewModel();
        vm.IsAssetsPanelVisible = false;
        vm.SelectedAssetsPane = AssetsPaneKind.Tools;
        vm.QuickStartRecentConnections.Clear();
        vm.FilteredQuickStartRecentConnections.Clear();

        vm.LocateHostFromQuickStartCommand.Execute(null);

        Assert.True(vm.IsAssetsPanelVisible);
        Assert.Equal(AssetsPaneKind.Hosts, vm.SelectedAssetsPane);
        Assert.Null(vm.PendingQuickStartLocateTarget);
        Assert.Contains("Hosts", vm.LastAssetActionMessage, StringComparison.Ordinal);
        Assert.Contains("Quick Start", vm.LastAssetActionMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void LocateHostFromQuickStart_TreeMode_ExpandsAncestorsAndSelectsTarget()
    {
        var vm = new MainWindowViewModel();
        var target = vm.FilteredQuickStartRecentConnections.First(node =>
            string.Equals(node.DisplayName, "prometheus", StringComparison.OrdinalIgnoreCase));

        vm.CollapseAllAssetsCommand.Execute(null);
        var shared = Flatten(vm.CurrentAssetTree).OfType<FolderNode>().First(node =>
            string.Equals(node.Name, "Shared Services", StringComparison.Ordinal));
        var observability = Flatten(vm.CurrentAssetTree).OfType<FolderNode>().First(node =>
            string.Equals(node.Name, "Observability", StringComparison.Ordinal));
        Assert.False(shared.IsExpanded);
        Assert.False(observability.IsExpanded);

        vm.OpenQuickStartConnectionCommand.Execute(target);
        vm.LocateHostFromQuickStartCommand.Execute(null);

        var selected = Assert.IsType<ConnectionNode>(vm.SelectedAssetNode);
        Assert.Equal("prometheus", selected.Name, ignoreCase: true);
        Assert.True(shared.IsExpanded);
        Assert.True(observability.IsExpanded);
        Assert.True(selected.QuickLocateHighlightOpacity > 0d);
        Assert.Contains("prometheus", vm.LastAssetActionMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LocateHostFromQuickStart_TreeMode_TargetNotInTree_SetsPendingAndKeepsSelectionUnchanged()
    {
        var vm = new MainWindowViewModel();
        var previousSelection = vm.SelectedAssetNode;
        var orphanRecent = new QuickStartRecentConnection(
            assetId: "missing-node-id",
            displayName: "orphan-host",
            host: "198.51.100.88",
            username: "ghost",
            port: 2222,
            lastUsedAt: DateTimeOffset.Now);

        vm.QuickStartRecentConnections.Clear();
        vm.FilteredQuickStartRecentConnections.Clear();
        vm.QuickStartRecentConnections.Add(orphanRecent);
        vm.FilteredQuickStartRecentConnections.Add(orphanRecent);

        vm.LocateHostFromQuickStartCommand.Execute(null);

        var pending = Assert.IsType<ConnectionNode>(vm.PendingQuickStartLocateTarget);
        Assert.Equal(orphanRecent.Host, pending.Host, ignoreCase: true);
        Assert.Equal(orphanRecent.Username, pending.User, ignoreCase: true);
        Assert.Equal(orphanRecent.Port, pending.Port);
        Assert.Same(previousSelection, vm.SelectedAssetNode);
        Assert.Contains("已解析目标 Host", vm.LastAssetActionMessage, StringComparison.Ordinal);
        Assert.Contains(orphanRecent.DisplayName, vm.LastAssetActionMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LocateHostFromQuickStart_FlatMode_SelectsOnlyTargetAndHighlightFallsBack()
    {
        var vm = new MainWindowViewModel();
        vm.SetListViewModeCommand.Execute(null);
        Assert.True(vm.IsFlatViewMode);

        var baselineSelected = vm.CurrentAssetFlatList.OfType<ConnectionNode>().First(node =>
            !string.Equals(node.Name, "stage-api", StringComparison.OrdinalIgnoreCase));
        vm.SetSelectedAssets([baselineSelected]);
        Assert.Single(vm.SelectedAssetNodes);
        Assert.Same(baselineSelected, vm.SelectedAssetNodes[0]);

        var targetRecent = vm.FilteredQuickStartRecentConnections.First(node =>
            string.Equals(node.DisplayName, "stage-api", StringComparison.OrdinalIgnoreCase));
        vm.OpenQuickStartConnectionCommand.Execute(targetRecent);

        vm.LocateHostFromQuickStartCommand.Execute(null);

        var selected = Assert.IsType<ConnectionNode>(vm.SelectedAssetNode);
        Assert.Equal("stage-api", selected.Name, ignoreCase: true);
        Assert.Single(vm.SelectedAssetNodes);
        Assert.Same(selected, vm.SelectedAssetNodes[0]);
        Assert.Same(selected, vm.PendingQuickStartLocateTarget);
        Assert.True(selected.QuickLocateHighlightOpacity > 0d);

        await Task.Delay(TimeSpan.FromMilliseconds(1500));

        Assert.Equal(0d, selected.QuickLocateHighlightOpacity);
        Assert.Same(selected, vm.SelectedAssetNode);
        Assert.Single(vm.SelectedAssetNodes);
        Assert.Same(selected, vm.SelectedAssetNodes[0]);
    }

    [Fact]
    public async Task LocateHostFromQuickStart_FlatMode_ConsecutiveLocatesCancelPreviousHighlight()
    {
        var vm = new MainWindowViewModel();
        vm.SetListViewModeCommand.Execute(null);
        Assert.True(vm.IsFlatViewMode);

        var firstRecent = vm.FilteredQuickStartRecentConnections.First(node =>
            string.Equals(node.DisplayName, "core-gateway", StringComparison.OrdinalIgnoreCase));
        var secondRecent = vm.FilteredQuickStartRecentConnections.First(node =>
            string.Equals(node.DisplayName, "stage-api", StringComparison.OrdinalIgnoreCase));

        vm.OpenQuickStartConnectionCommand.Execute(firstRecent);
        vm.LocateHostFromQuickStartCommand.Execute(null);
        var firstTarget = Assert.IsType<ConnectionNode>(vm.SelectedAssetNode);
        Assert.True(firstTarget.QuickLocateHighlightOpacity > 0d);

        vm.OpenQuickStartConnectionCommand.Execute(secondRecent);
        vm.LocateHostFromQuickStartCommand.Execute(null);
        var secondTarget = Assert.IsType<ConnectionNode>(vm.SelectedAssetNode);

        Assert.False(ReferenceEquals(firstTarget, secondTarget));
        Assert.Equal(0d, firstTarget.QuickLocateHighlightOpacity);
        Assert.True(secondTarget.QuickLocateHighlightOpacity > 0d);
        Assert.Same(secondTarget, vm.PendingQuickStartLocateTarget);
        Assert.Single(vm.SelectedAssetNodes);
        Assert.Same(secondTarget, vm.SelectedAssetNodes[0]);

        await Task.Delay(TimeSpan.FromMilliseconds(1500));

        Assert.Equal(0d, secondTarget.QuickLocateHighlightOpacity);
        Assert.Same(secondTarget, vm.SelectedAssetNode);
    }

    private static IEnumerable<AssetNode> Flatten(IEnumerable<AssetNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
            {
                yield return child;
            }
        }
    }
}
