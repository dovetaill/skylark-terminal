using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services.Mock;

public sealed class MockTerminalCommandBridge : ITerminalCommandBridge
{
    public bool PasteToActiveResult { get; set; } = true;

    public bool RunInActiveResult { get; set; } = true;

    public SnippetDispatchResult RunInAllTabsResult { get; set; } = SnippetDispatchResult.Empty;

    public List<(string Operation, string? TabId, string Content)> Calls { get; } = [];

    public List<string> RunInAllTabsTabIds { get; } = [];

    public Task<bool> PasteToActiveAsync(
        WorkspaceTabItemViewModel? tab,
        string content,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        Calls.Add(("paste", tab?.Id, content));
        return Task.FromResult(PasteToActiveResult);
    }

    public Task<bool> RunInActiveAsync(
        WorkspaceTabItemViewModel? tab,
        string content,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        Calls.Add(("run-active", tab?.Id, content));
        return Task.FromResult(RunInActiveResult);
    }

    public Task<SnippetDispatchResult> RunInAllTabsAsync(
        IEnumerable<WorkspaceTabItemViewModel> tabs,
        string content,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        Calls.Add(("run-all", null, content));
        RunInAllTabsTabIds.Clear();
        RunInAllTabsTabIds.AddRange(tabs.Select(tab => tab.Id));
        return Task.FromResult(RunInAllTabsResult);
    }
}
