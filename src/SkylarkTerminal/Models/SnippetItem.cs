using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SkylarkTerminal.Models;

public sealed partial class SnippetItem : ObservableObject
{
    private string title = string.Empty;
    private string content = string.Empty;
    private List<string> tags = [];
    private int sortOrder;
    private DateTimeOffset createdAt = DateTimeOffset.UtcNow;
    private DateTimeOffset updatedAt = DateTimeOffset.UtcNow;
    private bool isExpanded;

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    public string Title
    {
        get => title;
        set => SetProperty(ref title, value);
    }

    public string Content
    {
        get => content;
        set
        {
            if (!SetProperty(ref content, value))
            {
                return;
            }

            OnPropertyChanged(nameof(PreviewText));
        }
    }

    public List<string> Tags
    {
        get => tags;
        set => SetProperty(ref tags, value ?? []);
    }

    public int SortOrder
    {
        get => sortOrder;
        set => SetProperty(ref sortOrder, value);
    }

    public DateTimeOffset CreatedAt
    {
        get => createdAt;
        set => SetProperty(ref createdAt, value);
    }

    public DateTimeOffset UpdatedAt
    {
        get => updatedAt;
        set => SetProperty(ref updatedAt, value);
    }

    [JsonIgnore]
    public bool IsExpanded
    {
        get => isExpanded;
        set => SetProperty(ref isExpanded, value);
    }

    [JsonIgnore]
    public string PreviewText => Content.Length <= 24 ? Content : $"{Content[..24]}...";
}
