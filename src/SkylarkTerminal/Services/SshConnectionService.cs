using Renci.SshNet;
using SkylarkTerminal.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.Services;

public sealed class SshConnectionService : ISshConnectionService, IDisposable
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, SshClient> _clients = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ISshTerminalSession> _terminalSessions = new(StringComparer.Ordinal);

    public async Task<bool> ConnectAsync(ConnectionConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ConnectionId) ||
            string.IsNullOrWhiteSpace(config.Host) ||
            string.IsNullOrWhiteSpace(config.Username))
        {
            RuntimeLogger.Warn(
                "ssh-real",
                $"Connect skipped for invalid config. id={config.ConnectionId}, host={config.Host}, user={config.Username}");
            return false;
        }

        await DisconnectAsync(config.ConnectionId).ConfigureAwait(false);

        SshClient? client = null;
        try
        {
            client = CreateClient(config);
            RuntimeLogger.Info(
                "ssh-real",
                $"Connect begin. id={config.ConnectionId}, host={config.Host}, port={config.Port}, user={config.Username}");
            await Task.Run(client.Connect).ConfigureAwait(false);
            if (!client.IsConnected)
            {
                RuntimeLogger.Warn(
                    "ssh-real",
                    $"Connect finished but not connected. id={config.ConnectionId}, host={config.Host}");
                client.Dispose();
                return false;
            }

            lock (_syncRoot)
            {
                _clients[config.ConnectionId] = client;
            }

            RuntimeLogger.Info(
                "ssh-real",
                $"Connect success. id={config.ConnectionId}, host={config.Host}, server_version={client.ConnectionInfo.ServerVersion}");
            return true;
        }
        catch (Exception ex)
        {
            RuntimeLogger.Error(
                "ssh-real",
                $"Connect exception. id={config.ConnectionId}, host={config.Host}, port={config.Port}",
                ex);
            client?.Dispose();
            return false;
        }
    }

    public async Task DisconnectAsync(string connectionId)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return;
        }

        ISshTerminalSession? terminalSession = null;
        SshClient? client = null;

        lock (_syncRoot)
        {
            if (_terminalSessions.TryGetValue(connectionId, out var session))
            {
                terminalSession = session;
                _terminalSessions.Remove(connectionId);
            }

            if (_clients.TryGetValue(connectionId, out var existing))
            {
                client = existing;
                _clients.Remove(connectionId);
            }
        }

        if (terminalSession is not null)
        {
            try
            {
                terminalSession.Closed -= OnTerminalSessionClosed;
                terminalSession.Faulted -= OnTerminalSessionFaulted;
                await terminalSession.DisconnectAsync().ConfigureAwait(false);
                RuntimeLogger.Info("ssh-real", $"Terminal session disconnected. id={connectionId}");
            }
            catch (Exception ex)
            {
                RuntimeLogger.Error("ssh-real", $"Terminal session disconnect exception. id={connectionId}", ex);
            }
            finally
            {
                terminalSession.Dispose();
            }
        }

        if (client is null)
        {
            return;
        }

        try
        {
            await Task.Run(() =>
            {
                if (client.IsConnected)
                {
                    client.Disconnect();
                }
            }).ConfigureAwait(false);
            RuntimeLogger.Info("ssh-real", $"Disconnect success. id={connectionId}");
        }
        catch (Exception ex)
        {
            RuntimeLogger.Error("ssh-real", $"Disconnect exception. id={connectionId}", ex);
        }
        finally
        {
            client.Dispose();
        }
    }

    public async Task RunCommandAsync(string connectionId, string command)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || string.IsNullOrWhiteSpace(command))
        {
            return;
        }

        var client = ResolveConnectedClient(connectionId);
        if (client is null)
        {
            RuntimeLogger.Warn("ssh-real", $"RunCommand skipped because connection is not active. id={connectionId}");
            return;
        }

        try
        {
            var result = await Task.Run(() => client.RunCommand(command)).ConfigureAwait(false);
            var outputSnippet = (result.Result ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
            if (outputSnippet.Length > 180)
            {
                outputSnippet = outputSnippet[..180] + "...";
            }

            RuntimeLogger.Info(
                "ssh-real",
                $"RunCommand completed. id={connectionId}, exit={result.ExitStatus}, command={command}, output={outputSnippet}");
        }
        catch (Exception ex)
        {
            RuntimeLogger.Error("ssh-real", $"RunCommand exception. id={connectionId}, command={command}", ex);
        }
    }

    public async Task<ISshTerminalSession> CreateTerminalSessionAsync(
        ConnectionConfig config,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(config.ConnectionId) ||
            string.IsNullOrWhiteSpace(config.Host) ||
            string.IsNullOrWhiteSpace(config.Username))
        {
            throw new InvalidOperationException(
                $"Invalid terminal config. id={config.ConnectionId}, host={config.Host}, user={config.Username}");
        }

        await DisconnectAsync(config.ConnectionId).ConfigureAwait(false);

        var session = await SshTerminalSession.CreateAsync(config, cancellationToken).ConfigureAwait(false);
        session.Closed += OnTerminalSessionClosed;
        session.Faulted += OnTerminalSessionFaulted;

        lock (_syncRoot)
        {
            _terminalSessions[config.ConnectionId] = session;
        }

        RuntimeLogger.Info(
            "ssh-real",
            $"Terminal session created. id={config.ConnectionId}, host={config.Host}, port={config.Port}");
        return session;
    }

    public void Dispose()
    {
        List<ISshTerminalSession> sessions;
        List<SshClient> clients;
        lock (_syncRoot)
        {
            sessions = [.. _terminalSessions.Values];
            clients = [.. _clients.Values];
            _terminalSessions.Clear();
            _clients.Clear();
        }

        foreach (var session in sessions)
        {
            try
            {
                session.Closed -= OnTerminalSessionClosed;
                session.Faulted -= OnTerminalSessionFaulted;
                session.Dispose();
            }
            catch
            {
            }
        }

        foreach (var client in clients)
        {
            try
            {
                if (client.IsConnected)
                {
                    client.Disconnect();
                }
            }
            catch
            {
            }
            finally
            {
                client.Dispose();
            }
        }
    }

    private void OnTerminalSessionClosed(object? sender, string reason)
    {
        if (sender is not ISshTerminalSession session)
        {
            return;
        }

        lock (_syncRoot)
        {
            _terminalSessions.Remove(session.SessionId);
        }

        RuntimeLogger.Info("ssh-real", $"Terminal session closed. id={session.SessionId}, reason={reason}");
    }

    private void OnTerminalSessionFaulted(object? sender, Exception exception)
    {
        if (sender is not ISshTerminalSession session)
        {
            return;
        }

        RuntimeLogger.Error("ssh-real", $"Terminal session faulted. id={session.SessionId}", exception);
    }

    private static SshClient CreateClient(ConnectionConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.PrivateKeyPath) && File.Exists(config.PrivateKeyPath))
        {
            var keyFile = string.IsNullOrWhiteSpace(config.Password)
                ? new PrivateKeyFile(config.PrivateKeyPath)
                : new PrivateKeyFile(config.PrivateKeyPath, config.Password);

            var clientWithKey = new SshClient(config.Host, config.Port, config.Username, keyFile);
            clientWithKey.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
            clientWithKey.KeepAliveInterval = TimeSpan.FromSeconds(20);
            return clientWithKey;
        }

        var clientWithPassword = new SshClient(config.Host, config.Port, config.Username, config.Password ?? string.Empty);
        clientWithPassword.ConnectionInfo.Timeout = TimeSpan.FromSeconds(10);
        clientWithPassword.KeepAliveInterval = TimeSpan.FromSeconds(20);
        return clientWithPassword;
    }

    private SshClient? ResolveConnectedClient(string connectionId)
    {
        lock (_syncRoot)
        {
            if (_clients.TryGetValue(connectionId, out var client) && client.IsConnected)
            {
                return client;
            }
        }

        return null;
    }

    private sealed class SshTerminalSession : ISshTerminalSession
    {
        private readonly ConnectionConfig _config;
        private readonly SshClient _client;
        private readonly ShellStream _shellStream;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private readonly CancellationTokenSource _readLoopCts = new();
        private readonly Task _readLoopTask;
        private int _closedRaised;
        private int _disposed;

        private SshTerminalSession(
            ConnectionConfig config,
            SshClient client,
            ShellStream shellStream)
        {
            _config = config;
            _client = client;
            _shellStream = shellStream;
            _readLoopTask = Task.Run(ReadLoopAsync);
        }

        public static async Task<ISshTerminalSession> CreateAsync(
            ConnectionConfig config,
            CancellationToken cancellationToken)
        {
            var client = CreateClient(config);
            try
            {
                RuntimeLogger.Info(
                    "ssh-real",
                    $"Terminal connect begin. id={config.ConnectionId}, host={config.Host}, port={config.Port}, user={config.Username}");
                await Task.Run(client.Connect, cancellationToken).ConfigureAwait(false);
                if (!client.IsConnected)
                {
                    throw new InvalidOperationException($"SSH terminal connect failed. id={config.ConnectionId}");
                }

                var shellStream = client.CreateShellStream(
                    terminalName: "xterm-256color",
                    columns: 120,
                    rows: 36,
                    width: 1280,
                    height: 720,
                    bufferSize: 4096);

                RuntimeLogger.Info(
                    "ssh-real",
                    $"Terminal connect success. id={config.ConnectionId}, host={config.Host}");

                return new SshTerminalSession(config, client, shellStream);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        public string SessionId => _config.ConnectionId;

        public bool IsConnected => _client.IsConnected;

        public event EventHandler<string>? OutputReceived;

        public event EventHandler<string>? Closed;

        public event EventHandler<Exception>? Faulted;

        public async Task SendAsync(string data, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(data) || !IsConnected)
            {
                return;
            }

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var payload = Encoding.UTF8.GetBytes(data);
                await _shellStream.WriteAsync(payload, 0, payload.Length, cancellationToken).ConfigureAwait(false);
                await _shellStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task ResizeAsync(
            uint columns,
            uint rows,
            uint widthPixels,
            uint heightPixels,
            CancellationToken cancellationToken = default)
        {
            if (!IsConnected)
            {
                return;
            }

            await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                _shellStream.ChangeWindowSize(columns, rows, widthPixels, heightPixels);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public async Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 1)
            {
                return;
            }

            _readLoopCts.Cancel();

            try
            {
                await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    if (_client.IsConnected)
                    {
                        _client.Disconnect();
                    }
                }
                finally
                {
                    _writeLock.Release();
                }
            }
            catch
            {
            }

            await CompleteShutdownAsync("Disconnected by client.").ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            _readLoopCts.Cancel();
            try
            {
                _readLoopTask.GetAwaiter().GetResult();
            }
            catch
            {
            }

            try
            {
                if (_client.IsConnected)
                {
                    _client.Disconnect();
                }
            }
            catch
            {
            }

            _shellStream.Dispose();
            _client.Dispose();
            _writeLock.Dispose();
            _readLoopCts.Dispose();
        }

        private async Task ReadLoopAsync()
        {
            var buffer = new byte[8192];
            try
            {
                while (!_readLoopCts.IsCancellationRequested && _client.IsConnected)
                {
                    if (!_shellStream.DataAvailable)
                    {
                        await Task.Delay(12, _readLoopCts.Token).ConfigureAwait(false);
                        continue;
                    }

                    var bytesRead = await _shellStream.ReadAsync(
                        buffer,
                        0,
                        buffer.Length,
                        _readLoopCts.Token).ConfigureAwait(false);
                    if (bytesRead <= 0)
                    {
                        await Task.Delay(10, _readLoopCts.Token).ConfigureAwait(false);
                        continue;
                    }

                    var payload = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    OutputReceived?.Invoke(this, payload);
                }

                await CompleteShutdownAsync("Remote session ended.").ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await CompleteShutdownAsync("Read loop cancelled.").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Faulted?.Invoke(this, ex);
                RuntimeLogger.Error(
                    "ssh-real",
                    $"Terminal read loop exception. id={_config.ConnectionId}, host={_config.Host}",
                    ex);
                await CompleteShutdownAsync($"Read loop faulted: {ex.Message}").ConfigureAwait(false);
            }
        }

        private async Task CompleteShutdownAsync(string reason)
        {
            if (Interlocked.CompareExchange(ref _disposed, 0, 0) == 0)
            {
                try
                {
                    if (_client.IsConnected)
                    {
                        _client.Disconnect();
                    }
                }
                catch
                {
                }
            }

            if (Interlocked.Exchange(ref _closedRaised, 1) == 0)
            {
                Closed?.Invoke(this, reason);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
