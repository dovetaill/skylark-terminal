using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public interface ITerminalCommandBridge
{
    Task<bool> PasteToActiveAsync(
        WorkspaceTabItemViewModel? tab,
        string content,
        CancellationToken cancellationToken = default);

    Task<bool> RunInActiveAsync(
        WorkspaceTabItemViewModel? tab,
        string content,
        CancellationToken cancellationToken = default);

    Task<SnippetDispatchResult> RunInAllTabsAsync(
        IEnumerable<WorkspaceTabItemViewModel> tabs,
        string content,
        CancellationToken cancellationToken = default);
}
