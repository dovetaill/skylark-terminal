using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace SkylarkTerminal.Models;

public sealed partial class SnippetCategory : ObservableObject
{
    private string name = string.Empty;
    private int sortOrder;
    private ObservableCollection<SnippetItem> items = [];
    private bool isExpanded = true;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public int SortOrder
    {
        get => sortOrder;
        set => SetProperty(ref sortOrder, value);
    }

    public ObservableCollection<SnippetItem> Items
    {
        get => items;
        set => SetProperty(ref items, value ?? []);
    }

    [JsonIgnore]
    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }
}
