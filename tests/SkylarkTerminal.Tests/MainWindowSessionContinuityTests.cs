using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels;
using System.Threading;

namespace SkylarkTerminal.Tests;

public class MainWindowSessionContinuityTests
{
    [Fact]
    public async Task CompleteWorkspaceDragDrop_CrossPaneMove_KeepsSameSessionHandleForSameTabId()
    {
        var (vm, registry, sshService) = CreateVmWithRealRegistry();
        var tab = vm.WorkspaceTabs[0];
        var handle = await registry.GetOrCreateAsync(tab.Id, CreateConfig("conn-move"));
        var session = Assert.IsType<TrackingSshTerminalSession>(handle.Session);

        var pane1 = Assert.Single(vm.WorkspacePanes);
        vm.BeginWorkspaceDragPreview(pane1.PaneId, tab.Id, tab);
        Assert.True(vm.CompleteWorkspaceDragDrop(pane1.PaneId, WorkspaceDropDirection.Right));

        var pane2 = vm.WorkspacePanes.Single(p => p.Tabs.Contains(tab));
        vm.BeginWorkspaceDragPreview(pane2.PaneId, tab.Id, tab);
        var moved = vm.CompleteWorkspaceDragDrop(pane1.PaneId, dropDirection: null, targetIndex: 0);

        Assert.True(moved);
        Assert.True(registry.TryGet(tab.Id, out var stored));
        Assert.Same(handle, stored);
        Assert.Equal(1, sshService.CreateTerminalSessionCallCount);
        Assert.Equal(0, session.DisconnectCallCount);
        Assert.Equal(0, session.DisposeCallCount);
    }

    [Fact]
    public async Task CompleteWorkspaceDragDrop_Split_DoesNotTriggerReconnectOrDisconnect()
    {
        var (vm, registry, sshService) = CreateVmWithRealRegistry();
        var tab = vm.WorkspaceTabs[0];
        var handle = await registry.GetOrCreateAsync(tab.Id, CreateConfig("conn-split"));
        var session = Assert.IsType<TrackingSshTerminalSession>(handle.Session);

        var pane1 = Assert.Single(vm.WorkspacePanes);
        vm.BeginWorkspaceDragPreview(pane1.PaneId, tab.Id, tab);
        var accepted = vm.CompleteWorkspaceDragDrop(pane1.PaneId, WorkspaceDropDirection.Left);

        Assert.True(accepted);
        Assert.True(registry.TryGet(tab.Id, out var stored));
        Assert.Same(handle, stored);
        Assert.Equal(1, sshService.CreateTerminalSessionCallCount);
        Assert.Equal(0, session.DisconnectCallCount);
        Assert.Equal(0, session.DisposeCallCount);
    }

    [Fact]
    public async Task CloseTab_DisposesSessionOnlyWhenTabCloses()
    {
        var (vm, registry, sshService) = CreateVmWithRealRegistry();
        var tab = vm.WorkspaceTabs[0];
        var handle = await registry.GetOrCreateAsync(tab.Id, CreateConfig("conn-close"));
        var session = Assert.IsType<TrackingSshTerminalSession>(handle.Session);

        vm.CloseTabCommand.Execute(tab);

        Assert.False(registry.TryGet(tab.Id, out _));
        Assert.Equal(1, sshService.CreateTerminalSessionCallCount);
        Assert.Equal(1, session.DisconnectCallCount);
        Assert.Equal(1, session.DisposeCallCount);
    }

    private static (MainWindowViewModel Vm, SessionRegistryService Registry, TrackingSshConnectionService SshService)
        CreateVmWithRealRegistry()
    {
        var sshService = new TrackingSshConnectionService();
        var registry = new SessionRegistryService(sshService);

        var vm = new MainWindowViewModel(
            new MockAssetCatalogService(),
            sshService,
            new MockSftpService(),
            new MockAppDialogService(),
            new MockClipboardService(),
            registry,
            new WorkspaceLayoutService(),
            new DragSessionService());

        return (vm, registry, sshService);
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

    private sealed class TrackingSshConnectionService : ISshConnectionService
    {
        public int CreateTerminalSessionCallCount { get; private set; }

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

            CreateTerminalSessionCallCount++;
            ISshTerminalSession session = new TrackingSshTerminalSession(
                $"session-{CreateTerminalSessionCallCount}");
            return Task.FromResult(session);
        }
    }

    private sealed class TrackingSshTerminalSession : ISshTerminalSession
    {
        private bool _connected = true;

        public TrackingSshTerminalSession(string sessionId)
        {
            SessionId = sessionId;
        }

        public string SessionId { get; }

        public bool IsConnected => _connected;

        public int DisconnectCallCount { get; private set; }

        public int DisposeCallCount { get; private set; }

        public event EventHandler<string>? OutputReceived;

        public event EventHandler<string>? Closed;

        public event EventHandler<Exception>? Faulted;

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
            _connected = false;
            Closed?.Invoke(this, "disconnected");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            DisposeCallCount++;
            _connected = false;
        }
    }
}
