using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services.Mock;

public sealed class MockSshConnectionService : ISshConnectionService
{
    public Task<bool> ConnectAsync(ConnectionConfig config)
    {
        var isConnected = !string.IsNullOrWhiteSpace(config.Host);
        RuntimeLogger.Info(
            "ssh-mock",
            $"Connect requested. id={config.ConnectionId}, host={config.Host}, port={config.Port}, user={config.Username}, result={(isConnected ? "success" : "failed")}");
        return Task.FromResult(isConnected);
    }

    public Task DisconnectAsync(string connectionId)
    {
        RuntimeLogger.Info("ssh-mock", $"Disconnect requested. id={connectionId}");
        return Task.CompletedTask;
    }

    public Task RunCommandAsync(string connectionId, string command)
    {
        RuntimeLogger.Info("ssh-mock", $"RunCommand requested. id={connectionId}, command={command}");
        return Task.CompletedTask;
    }

    public Task<ISshTerminalSession> CreateTerminalSessionAsync(
        ConnectionConfig config,
        CancellationToken cancellationToken = default)
    {
        RuntimeLogger.Info(
            "ssh-mock",
            $"Create terminal session requested. id={config.ConnectionId}, host={config.Host}, port={config.Port}, user={config.Username}");

        ISshTerminalSession session = new MockSshTerminalSession(config.ConnectionId);
        return Task.FromResult(session);
    }

    private sealed class MockSshTerminalSession : ISshTerminalSession
    {
        private bool _disposed;

        public MockSshTerminalSession(string sessionId)
        {
            SessionId = sessionId;
            _ = Task.Run(async () =>
            {
                await Task.Delay(50).ConfigureAwait(false);
                OutputReceived?.Invoke(
                    this,
                    "Mock terminal session connected.\r\nType commands here when real SSH is enabled.\r\n");
            });
        }

        public string SessionId { get; }

        public bool IsConnected => !_disposed;

        public event EventHandler<string>? OutputReceived;

        public event EventHandler<string>? Closed;

        public event EventHandler<Exception>? Faulted;

        public Task SendAsync(string data, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            OutputReceived?.Invoke(this, data);
            return Task.CompletedTask;
        }

        public Task ResizeAsync(
            uint columns,
            uint rows,
            uint widthPixels,
            uint heightPixels,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            RuntimeLogger.Info(
                "ssh-mock",
                $"Resize session. id={SessionId}, cols={columns}, rows={rows}, width={widthPixels}, height={heightPixels}");
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            _disposed = true;
            Closed?.Invoke(this, "Mock session disconnected.");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Closed?.Invoke(this, "Mock session disposed.");
        }
    }
}
