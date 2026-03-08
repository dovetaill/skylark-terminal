using SkylarkTerminal.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public sealed class SessionRegistryService : ISessionRegistryService
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, ISshTerminalSessionHandle> _handlesByTabId = new(StringComparer.Ordinal);
    private readonly ISshConnectionService _sshConnectionService;

    public SessionRegistryService(ISshConnectionService sshConnectionService)
    {
        _sshConnectionService = sshConnectionService;
    }

    public async ValueTask<ISshTerminalSessionHandle> GetOrCreateAsync(
        string tabId,
        ConnectionConfig config,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tabId))
        {
            throw new ArgumentException("Tab id cannot be null or whitespace.", nameof(tabId));
        }

        ISshTerminalSessionHandle? staleHandle = null;
        if (TryGet(tabId, out var existing) && existing is not null)
        {
            if (existing.Session.IsConnected)
            {
                return existing;
            }

            TryDetach(tabId, out staleHandle);
        }

        if (staleHandle is not null)
        {
            await DisposeHandleAsync(staleHandle, cancellationToken).ConfigureAwait(false);
        }

        var session = await _sshConnectionService
            .CreateTerminalSessionAsync(config, cancellationToken)
            .ConfigureAwait(false);

        var created = new SshTerminalSessionHandle(tabId, session);
        lock (_syncRoot)
        {
            _handlesByTabId[tabId] = created;
        }

        return created;
    }

    public void Attach(string tabId, ISshTerminalSessionHandle handle)
    {
        if (string.IsNullOrWhiteSpace(tabId))
        {
            throw new ArgumentException("Tab id cannot be null or whitespace.", nameof(tabId));
        }

        ArgumentNullException.ThrowIfNull(handle);
        lock (_syncRoot)
        {
            _handlesByTabId[tabId] = handle;
        }
    }

    public bool TryGet(string tabId, out ISshTerminalSessionHandle? handle)
    {
        if (string.IsNullOrWhiteSpace(tabId))
        {
            handle = null;
            return false;
        }

        lock (_syncRoot)
        {
            return _handlesByTabId.TryGetValue(tabId, out handle);
        }
    }

    public bool TryDetach(string tabId, out ISshTerminalSessionHandle? handle)
    {
        if (string.IsNullOrWhiteSpace(tabId))
        {
            handle = null;
            return false;
        }

        lock (_syncRoot)
        {
            if (!_handlesByTabId.TryGetValue(tabId, out handle))
            {
                return false;
            }

            _handlesByTabId.Remove(tabId);
            return true;
        }
    }

    public async Task DisposeAsync(string tabId, CancellationToken cancellationToken = default)
    {
        if (!TryDetach(tabId, out var handle) || handle is null)
        {
            return;
        }

        await DisposeHandleAsync(handle, cancellationToken).ConfigureAwait(false);
    }

    private static async Task DisposeHandleAsync(
        ISshTerminalSessionHandle handle,
        CancellationToken cancellationToken)
    {
        try
        {
            await handle.Session.DisconnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RuntimeLogger.Error("session-registry", $"Dispose session failed. tab_id={handle.TabId}", ex);
        }
        finally
        {
            handle.Session.Dispose();
        }
    }

    private sealed class SshTerminalSessionHandle : ISshTerminalSessionHandle
    {
        public SshTerminalSessionHandle(string tabId, ISshTerminalSession session)
        {
            TabId = tabId;
            Session = session;
        }

        public string TabId { get; }

        public ISshTerminalSession Session { get; }
    }
}
