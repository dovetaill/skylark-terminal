using SkylarkTerminal.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public interface ISnippetRepository
{
    Task<IReadOnlyList<SnippetCategory>> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(IReadOnlyList<SnippetCategory> categories, CancellationToken cancellationToken = default);
}
