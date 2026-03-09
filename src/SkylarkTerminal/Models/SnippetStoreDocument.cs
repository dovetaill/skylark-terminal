using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace SkylarkTerminal.Models;

public sealed class SnippetStoreDocument
{
    public List<SnippetStoreCategoryDocument> Categories { get; set; } = [];

    public static SnippetStoreDocument FromRuntime(IReadOnlyList<SnippetCategory> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);

        return new SnippetStoreDocument
        {
            Categories =
            [
                .. categories.Select(category => new SnippetStoreCategoryDocument
                {
                    Id = category.Id,
                    Name = category.Name,
                    SortOrder = category.SortOrder,
                    Items =
                    [
                        .. category.Items.Select(item => new SnippetStoreItemDocument
                        {
                            Id = item.Id,
                            Title = item.Title,
                            Content = item.Content,
                            Tags = [.. item.Tags],
                            SortOrder = item.SortOrder,
                            CreatedAt = item.CreatedAt,
                            UpdatedAt = item.UpdatedAt,
                        }),
                    ],
                }),
            ],
        };
    }

    public List<SnippetCategory> ToRuntimeModel()
    {
        return
        [
            .. Categories.Select(category => new SnippetCategory
            {
                Id = category.Id,
                Name = category.Name,
                SortOrder = category.SortOrder,
                Items = new ObservableCollection<SnippetItem>(
                    category.Items.Select(item => new SnippetItem
                    {
                        Id = item.Id,
                        Title = item.Title,
                        Content = item.Content,
                        Tags = [.. item.Tags],
                        SortOrder = item.SortOrder,
                        CreatedAt = item.CreatedAt,
                        UpdatedAt = item.UpdatedAt,
                    })),
            }),
        ];
    }

    public sealed class SnippetStoreCategoryDocument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get; set; } = string.Empty;

        public int SortOrder { get; set; }

        public List<SnippetStoreItemDocument> Items { get; set; } = [];
    }

    public sealed class SnippetStoreItemDocument
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Title { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;

        public List<string> Tags { get; set; } = [];

        public int SortOrder { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    }
}
