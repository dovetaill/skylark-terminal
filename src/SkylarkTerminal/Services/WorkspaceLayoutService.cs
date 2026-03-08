using SkylarkTerminal.Models;
using System;
using System.Collections.Generic;

namespace SkylarkTerminal.Services;

public sealed class WorkspaceLayoutService : IWorkspaceLayoutService
{
    private readonly HashSet<string> _paneIds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _tabsByPane = new(StringComparer.Ordinal);
    private int _paneSeed = 1;

    public WorkspaceLayoutService()
    {
        InitializeRootPane("pane-1");
    }

    public WorkspaceLayoutNode Root { get; private set; } = new PaneNode("pane-1");

    public IReadOnlyCollection<string> PaneIds => _paneIds;

    public void InitializeRootPane(string paneId)
    {
        if (string.IsNullOrWhiteSpace(paneId))
        {
            throw new ArgumentException("Pane id cannot be null or whitespace.", nameof(paneId));
        }

        _paneIds.Clear();
        _tabsByPane.Clear();
        _paneIds.Add(paneId);
        _tabsByPane[paneId] = [];
        UpdatePaneSeed(paneId);
        Root = new PaneNode(paneId);
    }

    public bool MoveTab(string sourcePaneId, string targetPaneId, string tabId, int? index = null)
    {
        if (!IsKnownPane(sourcePaneId) ||
            !IsKnownPane(targetPaneId) ||
            string.IsNullOrWhiteSpace(tabId))
        {
            return false;
        }

        var sourceTabs = GetOrCreatePaneTabs(sourcePaneId);
        var targetTabs = GetOrCreatePaneTabs(targetPaneId);

        if (string.Equals(sourcePaneId, targetPaneId, StringComparison.Ordinal))
        {
            var currentIndex = sourceTabs.IndexOf(tabId);
            if (currentIndex >= 0)
            {
                sourceTabs.RemoveAt(currentIndex);
            }
            else
            {
                RuntimeLogger.Warn(
                    "workspace-layout",
                    $"Tab reorder found untracked tab. pane_id={sourcePaneId}, tab_id={tabId}");
            }

            var insertionIndex = NormalizeInsertionIndex(index, sourceTabs.Count);
            sourceTabs.Insert(insertionIndex, tabId);
            return true;
        }

        var wasTrackedInSource = sourceTabs.Remove(tabId);
        var duplicatedTargetIndex = targetTabs.IndexOf(tabId);
        if (duplicatedTargetIndex >= 0)
        {
            targetTabs.RemoveAt(duplicatedTargetIndex);
        }

        var targetIndex = NormalizeInsertionIndex(index, targetTabs.Count);
        targetTabs.Insert(targetIndex, tabId);

        if (!wasTrackedInSource)
        {
            RuntimeLogger.Warn(
                "workspace-layout",
                $"Cross-pane move found untracked source tab. source={sourcePaneId}, target={targetPaneId}, tab_id={tabId}");
        }

        return true;
    }

    public bool SplitAndMove(string sourcePaneId, string tabId, WorkspaceDropDirection dropDirection)
    {
        if (!IsKnownPane(sourcePaneId) || string.IsNullOrWhiteSpace(tabId))
        {
            return false;
        }

        var newPaneId = CreateNextPaneId();
        _paneIds.Add(newPaneId);
        _tabsByPane[newPaneId] = [tabId];

        var orientation = dropDirection is WorkspaceDropDirection.Left or WorkspaceDropDirection.Right
            ? WorkspaceSplitOrientation.Horizontal
            : WorkspaceSplitOrientation.Vertical;

        var sourcePane = new PaneNode(sourcePaneId);
        var newPane = new PaneNode(newPaneId);
        var first = dropDirection is WorkspaceDropDirection.Left or WorkspaceDropDirection.Top ? newPane : sourcePane;
        var second = ReferenceEquals(first, newPane) ? sourcePane : newPane;
        var replacementSplit = new SplitNode(
            nodeId: CreateSplitNodeId(),
            orientation: orientation,
            ratio: 0.5d,
            first: first,
            second: second);

        var updatedRoot = ReplacePaneNode(
            Root,
            sourcePaneId,
            _ => replacementSplit,
            out var replaced);
        if (!replaced || updatedRoot is null)
        {
            _paneIds.Remove(newPaneId);
            _tabsByPane.Remove(newPaneId);
            return false;
        }

        Root = updatedRoot;
        var sourceTabs = GetOrCreatePaneTabs(sourcePaneId);
        if (!sourceTabs.Remove(tabId))
        {
            RuntimeLogger.Warn(
                "workspace-layout",
                $"Split move found untracked source tab. source={sourcePaneId}, new={newPaneId}, tab_id={tabId}");
        }

        return true;
    }

    public bool RecyclePaneIfEmpty(string paneId)
    {
        if (!IsKnownPane(paneId) || _paneIds.Count <= 1)
        {
            return false;
        }

        if (GetOrCreatePaneTabs(paneId).Count > 0)
        {
            return false;
        }

        var updatedRoot = RemovePaneNode(Root, paneId, out var removed);
        if (!removed || updatedRoot is null)
        {
            return false;
        }

        Root = updatedRoot;
        Root = NormalizeTree(Root);
        _paneIds.Remove(paneId);
        _tabsByPane.Remove(paneId);

        return true;
    }

    private bool IsKnownPane(string paneId)
    {
        return !string.IsNullOrWhiteSpace(paneId) && _paneIds.Contains(paneId);
    }

    private static int NormalizeInsertionIndex(int? index, int itemCount)
    {
        if (!index.HasValue)
        {
            return itemCount;
        }

        if (index.Value <= 0)
        {
            return 0;
        }

        return index.Value >= itemCount ? itemCount : index.Value;
    }

    private List<string> GetOrCreatePaneTabs(string paneId)
    {
        if (!_tabsByPane.TryGetValue(paneId, out var tabs))
        {
            tabs = [];
            _tabsByPane[paneId] = tabs;
        }

        return tabs;
    }

    private void UpdatePaneSeed(string paneId)
    {
        const string prefix = "pane-";
        if (!paneId.StartsWith(prefix, StringComparison.Ordinal))
        {
            return;
        }

        var suffix = paneId[prefix.Length..];
        if (int.TryParse(suffix, out var value))
        {
            _paneSeed = Math.Max(_paneSeed, value);
        }
    }

    private string CreateNextPaneId()
    {
        while (true)
        {
            var candidate = $"pane-{++_paneSeed}";
            if (_paneIds.Add(candidate))
            {
                _paneIds.Remove(candidate);
                return candidate;
            }
        }
    }

    private static string CreateSplitNodeId()
    {
        return $"split-{Guid.NewGuid():N}";
    }

    private static WorkspaceLayoutNode? ReplacePaneNode(
        WorkspaceLayoutNode node,
        string paneId,
        Func<PaneNode, WorkspaceLayoutNode> replaceFactory,
        out bool replaced)
    {
        switch (node)
        {
            case PaneNode pane:
                if (string.Equals(pane.PaneId, paneId, StringComparison.Ordinal))
                {
                    replaced = true;
                    return replaceFactory(pane);
                }

                replaced = false;
                return node;

            case SplitNode split:
                var replacedFirst = ReplacePaneNode(split.First, paneId, replaceFactory, out var firstChanged);
                if (firstChanged && replacedFirst is not null)
                {
                    split.First = replacedFirst;
                    replaced = true;
                    return split;
                }

                var replacedSecond = ReplacePaneNode(split.Second, paneId, replaceFactory, out var secondChanged);
                if (secondChanged && replacedSecond is not null)
                {
                    split.Second = replacedSecond;
                    replaced = true;
                    return split;
                }

                replaced = false;
                return split;

            default:
                replaced = false;
                return node;
        }
    }

    private static WorkspaceLayoutNode? RemovePaneNode(
        WorkspaceLayoutNode node,
        string paneId,
        out bool removed)
    {
        switch (node)
        {
            case PaneNode pane:
                if (string.Equals(pane.PaneId, paneId, StringComparison.Ordinal))
                {
                    removed = true;
                    return null;
                }

                removed = false;
                return node;

            case SplitNode split:
                var first = RemovePaneNode(split.First, paneId, out var firstRemoved);
                if (firstRemoved)
                {
                    removed = true;
                    return first ?? split.Second;
                }

                var second = RemovePaneNode(split.Second, paneId, out var secondRemoved);
                if (secondRemoved)
                {
                    removed = true;
                    return second ?? split.First;
                }

                removed = false;
                return split;

            default:
                removed = false;
                return node;
        }
    }

    private static WorkspaceLayoutNode NormalizeTree(WorkspaceLayoutNode node)
    {
        if (node is not SplitNode split)
        {
            return node;
        }

        split.First = NormalizeTree(split.First);
        split.Second = NormalizeTree(split.Second);

        if (split.First is PaneNode firstPane &&
            split.Second is PaneNode secondPane &&
            string.Equals(firstPane.PaneId, secondPane.PaneId, StringComparison.Ordinal))
        {
            return firstPane;
        }

        return split;
    }
}
