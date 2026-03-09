using SkylarkTerminal.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services.Mock;

public sealed class MockSnippetRepository : ISnippetRepository
{
    private List<SnippetCategory> categories;

    public MockSnippetRepository(IReadOnlyList<SnippetCategory>? seed = null)
    {
        categories = Clone(seed ?? []);
    }

    public Task<IReadOnlyList<SnippetCategory>> LoadAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<SnippetCategory>>(Clone(categories));
    }

    public Task SaveAsync(IReadOnlyList<SnippetCategory> categories, CancellationToken cancellationToken = default)
    {
        this.categories = Clone(categories);
        return Task.CompletedTask;
    }

    private static List<SnippetCategory> Clone(IReadOnlyList<SnippetCategory> categories)
    {
        return SnippetStoreDocument.FromRuntime(categories).ToRuntimeModel().ToList();
    }
}
