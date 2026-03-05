using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SkylarkTerminal.Models;

public abstract partial class AssetNode : ObservableObject
{
    protected AssetNode(string id, string name, string kind, IEnumerable<AssetNode>? children = null)
    {
        Id = id;
        this.name = name;
        Kind = kind;
        Children = children is null
            ? new ObservableCollection<AssetNode>()
            : new ObservableCollection<AssetNode>(children);
    }

    public string Id { get; }

    [ObservableProperty]
    private string name;

    public string Kind { get; }

    [ObservableProperty]
    private bool isExpanded;

    [ObservableProperty]
    private bool isVisible = true;

    public ObservableCollection<AssetNode> Children { get; }

    public bool IsFolder => this is FolderNode;

    public string AssetTypeIcon => IsFolder ? "📁" : "🖥";

    public virtual bool Matches(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
               Kind.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
}
