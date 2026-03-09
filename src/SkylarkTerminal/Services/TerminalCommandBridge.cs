using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public sealed class TerminalCommandBridge : ITerminalCommandBridge
{
    private readonly ISessionRegistryService sessionRegistryService;

    public TerminalCommandBridge(ISessionRegistryService sessionRegistryService)
    {
        this.sessionRegistryService = sessionRegistryService;
    }

    public async Task<bool> PasteToActiveAsync(
        WorkspaceTabItemViewModel? tab,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetConnectedSession(tab, out var session))
        {
            return false;
        }

        await session.SendAsync(content, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RunInActiveAsync(
        WorkspaceTabItemViewModel? tab,
        string content,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetConnectedSession(tab, out var session))
        {
            return false;
        }

        await session.SendAsync(content + "\r", cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<SnippetDispatchResult> RunInAllTabsAsync(
        IEnumerable<WorkspaceTabItemViewModel> tabs,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tabs);

        var success = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var tab in tabs)
        {
            if (!TryGetConnectedSession(tab, out var session))
            {
                skipped++;
                continue;
            }

            try
            {
                await session.SendAsync(content + "\r", cancellationToken).ConfigureAwait(false);
                success++;
            }
            catch
            {
                failed++;
            }
        }

        return new SnippetDispatchResult(success, skipped, failed);
    }

    private bool TryGetConnectedSession(
        WorkspaceTabItemViewModel? tab,
        out ISshTerminalSession session)
    {
        session = null!;

        if (tab is null || tab.ConnectionConfig is null)
        {
            return false;
        }

        if (!sessionRegistryService.TryGet(tab.Id, out var handle) ||
            handle?.Session is not { IsConnected: true } connectedSession)
        {
            return false;
        }

        session = connectedSession;
        return true;
    }
}
