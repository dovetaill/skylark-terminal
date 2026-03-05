using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace SkylarkTerminal.Models;

public partial class AssetNode : ObservableObject
{
    public AssetNode(string id, string name, string kind, IEnumerable<AssetNode>? children = null)
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

    public ObservableCollection<AssetNode> Children { get; }
}
