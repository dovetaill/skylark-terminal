using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using System.Threading;

namespace SkylarkTerminal.Tests;

public class SessionRegistryServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_FirstCall_CreatesAndRegistersHandle()
    {
        var connectionService = new FakeSshConnectionService();
        var registry = new SessionRegistryService(connectionService);

        var handle = await registry.GetOrCreateAsync("tab-1", CreateConfig("conn-1"));

        Assert.Equal(1, connectionService.CreateCallCount);
        Assert.Equal("tab-1", handle.TabId);
        Assert.True(registry.TryGet("tab-1", out var stored));
        Assert.Same(handle, stored);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenExistingConnected_ReusesHandle()
    {
        var connectionService = new FakeSshConnectionService();
        var registry = new SessionRegistryService(connectionService);

        var first = await registry.GetOrCreateAsync("tab-1", CreateConfig("conn-1"));
        var second = await registry.GetOrCreateAsync("tab-1", CreateConfig("conn-2"));

        Assert.Equal(1, connectionService.CreateCallCount);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetOrCreateAsync_WhenExistingDisconnected_DisposesStaleAndRecreates()
    {
        var connectionService = new FakeSshConnectionService();
        var registry = new SessionRegistryService(connectionService);

        var first = await registry.GetOrCreateAsync("tab-1", CreateConfig("conn-1"));
        var firstSession = (FakeSshTerminalSession)first.Session;
        firstSession.SetConnected(false);

        var second = await registry.GetOrCreateAsync("tab-1", CreateConfig("conn-2"));
        var secondSession = (FakeSshTerminalSession)second.Session;

        Assert.Equal(2, connectionService.CreateCallCount);
        Assert.NotSame(first, second);
        Assert.NotSame(firstSession, secondSession);
        Assert.Equal(1, firstSession.DisconnectCallCount);
        Assert.Equal(1, firstSession.DisposeCallCount);
    }

    [Fact]
    public async Task DisposeAsync_DetachesAndDisposesSession()
    {
        var connectionService = new FakeSshConnectionService();
        var registry = new SessionRegistryService(connectionService);
        var handle = await registry.GetOrCreateAsync("tab-1", CreateConfig("conn-1"));
        var session = (FakeSshTerminalSession)handle.Session;

        await registry.DisposeAsync("tab-1");

        Assert.False(registry.TryGet("tab-1", out _));
        Assert.Equal(1, session.DisconnectCallCount);
        Assert.Equal(1, session.DisposeCallCount);
    }

    [Fact]
    public void AttachAndTryDetach_WorksForExternalHandle()
    {
        var connectionService = new FakeSshConnectionService();
        var registry = new SessionRegistryService(connectionService);
        var session = new FakeSshTerminalSession("manual");
        var handle = new FakeSshTerminalSessionHandle("tab-external", session);

        registry.Attach("tab-external", handle);

        Assert.True(registry.TryGet("tab-external", out var stored));
        Assert.Same(handle, stored);

        var detached = registry.TryDetach("tab-external", out var removed);
        Assert.True(detached);
        Assert.Same(handle, removed);
        Assert.False(registry.TryGet("tab-external", out _));
    }

    private static ConnectionConfig CreateConfig(string connectionId)
    {
        return new ConnectionConfig
        {
            ConnectionId = connectionId,
            Host = "127.0.0.1",
            Port = 22,
            Username = "tester",
            Password = "pwd",
        };
    }

    private sealed class FakeSshConnectionService : ISshConnectionService
    {
        public int CreateCallCount { get; private set; }

        public Task<bool> ConnectAsync(ConnectionConfig config)
        {
            _ = config;
            return Task.FromResult(true);
        }

        public Task DisconnectAsync(string connectionId)
        {
            _ = connectionId;
            return Task.CompletedTask;
        }

        public Task RunCommandAsync(string connectionId, string command)
        {
            _ = connectionId;
            _ = command;
            return Task.CompletedTask;
        }

        public Task<ISshTerminalSession> CreateTerminalSessionAsync(
            ConnectionConfig config,
            CancellationToken cancellationToken = default)
        {
            _ = config;
            _ = cancellationToken;
            CreateCallCount++;
            ISshTerminalSession session = new FakeSshTerminalSession($"session-{CreateCallCount}");
            return Task.FromResult(session);
        }
    }

    private sealed class FakeSshTerminalSession : ISshTerminalSession
    {
        private bool _isConnected = true;

        public FakeSshTerminalSession(string sessionId)
        {
            SessionId = sessionId;
        }

        public string SessionId { get; }

        public bool IsConnected => _isConnected;

        public int DisconnectCallCount { get; private set; }

        public int DisposeCallCount { get; private set; }

        public event EventHandler<string>? OutputReceived;

        public event EventHandler<string>? Closed;

        public event EventHandler<Exception>? Faulted;

        public void SetConnected(bool connected)
        {
            _isConnected = connected;
        }

        public Task SendAsync(string data, CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            OutputReceived?.Invoke(this, data);
            if (string.Equals(data, "__fault__", StringComparison.Ordinal))
            {
                Faulted?.Invoke(this, new InvalidOperationException("fault"));
            }
            return Task.CompletedTask;
        }

        public Task ResizeAsync(
            uint columns,
            uint rows,
            uint widthPixels,
            uint heightPixels,
            CancellationToken cancellationToken = default)
        {
            _ = columns;
            _ = rows;
            _ = widthPixels;
            _ = heightPixels;
            _ = cancellationToken;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            _ = cancellationToken;
            DisconnectCallCount++;
            _isConnected = false;
            Closed?.Invoke(this, "disconnected");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            DisposeCallCount++;
            _isConnected = false;
        }
    }

    private sealed class FakeSshTerminalSessionHandle : ISshTerminalSessionHandle
    {
        public FakeSshTerminalSessionHandle(string tabId, ISshTerminalSession session)
        {
            TabId = tabId;
            Session = session;
        }

        public string TabId { get; }

        public ISshTerminalSession Session { get; }
    }
}
