using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using Iciclecreek.Terminal;
using Microsoft.Extensions.DependencyInjection;
using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using SkylarkTerminal.Services.Mock;
using SkylarkTerminal.ViewModels;
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SkylarkTerminal.Views;

public partial class SshTerminalPane : UserControl
{
    public static readonly StyledProperty<WorkspaceTabItemViewModel?> TabProperty =
        AvaloniaProperty.Register<SshTerminalPane, WorkspaceTabItemViewModel?>(nameof(Tab));

    private readonly ISessionRegistryService _sessionRegistryService;
    private readonly ConcurrentQueue<string> _outputQueue = new();
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly DispatcherTimer _resizeDebounceTimer;
    private CancellationTokenSource? _connectCts;
    private ISshTerminalSession? _session;
    private int _flushScheduled;
    private int _lastColumns;
    private int _lastRows;
    private bool _terminalEventsHooked;
    private bool _isLoaded;
    private bool _firstPayloadLogged;
    private long _receivedChars;
    private int _receivedChunks;
    private DateTimeOffset _lastInputDropLogAt = DateTimeOffset.MinValue;
    private MainWindowViewModel? _hostViewModel;
    private string _tabIdForLogs = "<null>";
    private string _tabHostForLogs = "<none>";

    public SshTerminalPane()
    {
        InitializeComponent();
        var sshConnectionService = ResolveSshConnectionService();
        _sessionRegistryService = ResolveSessionRegistryService(sshConnectionService);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;

        _resizeDebounceTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(140),
        };
        _resizeDebounceTimer.Tick += OnResizeDebounceTick;
    }

    public WorkspaceTabItemViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TabProperty)
        {
            return;
        }

        if (change.NewValue is WorkspaceTabItemViewModel tab)
        {
            CaptureTabForLogs(tab);
            RuntimeLogger.Info(
                "terminal-ui",
                $"Tab binding changed. tab_id={tab.Id}, header={tab.Header}, host={tab.ConnectionConfig?.Host ?? "<none>"}");
        }

        if (_isLoaded)
        {
            _ = BeginConnectAsync(reconnect: false);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        AttachHostViewModel(ResolveHostViewModel());
        SyncTabFromHost("data-context-changed");
        EnsureTabContext("data-context-changed");
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _isLoaded = true;
        HookTerminalEvents();
        AttachHostViewModel(ResolveHostViewModel());
        SyncTabFromHost("loaded");
        EnsureTabContext("loaded");
        RefreshQuickStartBindings();
        TerminalHost.Focus();
        RuntimeLogger.Info(
            "terminal-ui",
            $"Pane loaded. tab_id={_tabIdForLogs}, host={_tabHostForLogs}, data_context={DataContext?.GetType().Name ?? "<null>"}");
        _ = BeginConnectAsync(reconnect: false);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _isLoaded = false;
        UnhookTerminalEvents();
        _resizeDebounceTimer.Stop();
        _connectCts?.Cancel();
        DetachSessionSubscriptions();
        AttachHostViewModel(null);
        RuntimeLogger.Info("terminal-ui", $"Pane unloaded. tab_id={_tabIdForLogs}");
    }

    private void HookTerminalEvents()
    {
        if (_terminalEventsHooked)
        {
            return;
        }

        TerminalHost.SizeChanged += OnTerminalSizeChanged;
        TerminalHost.PointerPressed += OnTerminalPointerPressed;
        TerminalHost.AddHandler(InputElement.KeyDownEvent, OnTerminalKeyDown, RoutingStrategies.Tunnel);
        TerminalHost.AddHandler(InputElement.TextInputEvent, OnTerminalTextInput, RoutingStrategies.Tunnel);
        _terminalEventsHooked = true;
    }

    private void UnhookTerminalEvents()
    {
        if (!_terminalEventsHooked)
        {
            return;
        }

        TerminalHost.SizeChanged -= OnTerminalSizeChanged;
        TerminalHost.PointerPressed -= OnTerminalPointerPressed;
        TerminalHost.RemoveHandler(InputElement.KeyDownEvent, OnTerminalKeyDown);
        TerminalHost.RemoveHandler(InputElement.TextInputEvent, OnTerminalTextInput);
        _terminalEventsHooked = false;
    }

    private async Task BeginConnectAsync(bool reconnect)
    {
        await _connectLock.WaitAsync();
        try
        {
            _connectCts?.Cancel();
            _connectCts = new CancellationTokenSource();
            var connectToken = _connectCts.Token;

            var tab = ResolveTab();
            var config = tab?.ConnectionConfig;
            if (tab is null || config is null)
            {
                DetachSessionSubscriptions();
                ShowQuickStart(tab);
                return;
            }

            if (reconnect)
            {
                DetachSessionSubscriptions();
                await _sessionRegistryService.DisposeAsync(tab.Id).ConfigureAwait(false);
            }
            else if (_session is not null &&
                     _session.IsConnected &&
                     string.Equals(_session.SessionId, config.ConnectionId, StringComparison.Ordinal))
            {
                SetState(
                    SessionState.Connected,
                    $"Connected to {config.Username}@{config.Host}:{config.Port}");
                _resizeDebounceTimer.Stop();
                _resizeDebounceTimer.Start();
                RuntimeLogger.Info(
                    "terminal-ui",
                    $"Reuse existing session. tab_id={tab.Id}, conn_id={config.ConnectionId}");
                return;
            }

            SetState(SessionState.Connecting, $"Connecting to {config.Host}:{config.Port} ...");

            RuntimeLogger.Info(
                "terminal-ui",
                $"Terminal connect begin. tab_id={tab.Id}, conn_id={config.ConnectionId}, host={config.Host}, port={config.Port}");

            var handle = await _sessionRegistryService
                .GetOrCreateAsync(tab.Id, config, connectToken)
                .ConfigureAwait(false);

            if (connectToken.IsCancellationRequested)
            {
                return;
            }

            AttachSessionSubscriptions(handle.Session);
            _lastColumns = 0;
            _lastRows = 0;
            _firstPayloadLogged = false;
            _receivedChars = 0;
            _receivedChunks = 0;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetState(
                    SessionState.Connected,
                    $"Connected to {config.Username}@{config.Host}:{config.Port}");
                TerminalHost.Focus();
                _resizeDebounceTimer.Stop();
                _resizeDebounceTimer.Start();
            });

            RuntimeLogger.Info(
                "terminal-ui",
                $"Terminal connect success. tab_id={tab.Id}, conn_id={config.ConnectionId}, host={config.Host}");
        }
        catch (OperationCanceledException)
        {
            RuntimeLogger.Warn("terminal-ui", "Terminal connect canceled.");
        }
        catch (Exception ex)
        {
            RuntimeLogger.Error("terminal-ui", "Terminal connect failed.", ex);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                SetState(SessionState.Faulted, $"Connection failed: {ex.Message}");
            });
        }
        finally
        {
            _connectLock.Release();
        }
    }

    private WorkspaceTabItemViewModel? ResolveTab()
    {
        SyncTabFromHost("resolve");
        EnsureTabContext("resolve");
        if (Tab is not null)
        {
            return Tab;
        }

        if (DataContext is WorkspaceTabItemViewModel vm)
        {
            Tab = vm;
            CaptureTabForLogs(vm);
            return vm;
        }

        return null;
    }

    private void EnsureTabContext(string reason)
    {
        SyncTabFromHost(reason);
        if (Tab is not null)
        {
            return;
        }

        if (DataContext is WorkspaceTabItemViewModel vmFromDataContext)
        {
            Tab = vmFromDataContext;
            CaptureTabForLogs(vmFromDataContext);
            RuntimeLogger.Info(
                "terminal-bind",
                $"Tab resolved from DataContext. reason={reason}, tab_id={vmFromDataContext.Id}, header={vmFromDataContext.Header}");
            return;
        }

        var tabItem = this.FindAncestorOfType<TabViewItem>();
        if (tabItem?.DataContext is WorkspaceTabItemViewModel vmFromTabItem)
        {
            Tab = vmFromTabItem;
            CaptureTabForLogs(vmFromTabItem);
            RuntimeLogger.Info(
                "terminal-bind",
                $"Tab resolved from TabViewItem ancestor. reason={reason}, tab_id={vmFromTabItem.Id}, header={vmFromTabItem.Header}");
            return;
        }

        RuntimeLogger.Warn(
            "terminal-bind",
            $"Tab resolve failed. reason={reason}, data_context={DataContext?.GetType().Name ?? "<null>"}, ancestor={(tabItem is null ? "<none>" : tabItem.GetType().Name)}");
    }

    private MainWindowViewModel? ResolveHostViewModel()
    {
        if (DataContext is MainWindowViewModel vmFromDataContext)
        {
            return vmFromDataContext;
        }

        if (this.FindAncestorOfType<Window>()?.DataContext is MainWindowViewModel vmFromWindow)
        {
            return vmFromWindow;
        }

        if (TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel vmFromTopLevel)
        {
            return vmFromTopLevel;
        }

        return null;
    }

    private void AttachHostViewModel(MainWindowViewModel? vm)
    {
        if (ReferenceEquals(_hostViewModel, vm))
        {
            return;
        }

        if (_hostViewModel is not null)
        {
            _hostViewModel.PropertyChanged -= OnHostViewModelPropertyChanged;
        }

        _hostViewModel = vm;
        if (_hostViewModel is not null)
        {
            _hostViewModel.PropertyChanged += OnHostViewModelPropertyChanged;
        }

        RefreshQuickStartBindings();
    }

    private void OnHostViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.SelectedWorkspaceTab) ||
            e.PropertyName == nameof(MainWindowViewModel.SelectedTab))
        {
            Dispatcher.UIThread.Post(() =>
            {
                SyncTabFromHost("selected-tab-changed");
            });
            return;
        }

        if (e.PropertyName == nameof(MainWindowViewModel.QuickStartSearchText) ||
            e.PropertyName == nameof(MainWindowViewModel.HasQuickStartRecentConnections) ||
            e.PropertyName == nameof(MainWindowViewModel.IsQuickStartRecentConnectionsEmpty))
        {
            Dispatcher.UIThread.Post(RefreshQuickStartBindings);
        }
    }

    private void SyncTabFromHost(string reason)
    {
        if (Tab is not null || DataContext is WorkspaceTabItemViewModel)
        {
            return;
        }

        if (_hostViewModel?.SelectedTab is not WorkspaceTabItemViewModel selected)
        {
            return;
        }

        if (ReferenceEquals(Tab, selected))
        {
            return;
        }

        Tab = selected;
        CaptureTabForLogs(selected);
        RuntimeLogger.Info(
            "terminal-bind",
            $"Tab resolved from MainWindowViewModel.SelectedTab. reason={reason}, tab_id={selected.Id}, header={selected.Header}, host={selected.ConnectionConfig?.Host ?? "<none>"}");
    }

    private void CaptureTabForLogs(WorkspaceTabItemViewModel? tab)
    {
        _tabIdForLogs = tab?.Id ?? "<null>";
        _tabHostForLogs = tab?.ConnectionConfig?.Host ?? "<none>";
    }

    private void AttachSessionSubscriptions(ISshTerminalSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        DetachSessionSubscriptions();
        session.OutputReceived += OnSessionOutputReceived;
        session.Closed += OnSessionClosed;
        session.Faulted += OnSessionFaulted;
        _session = session;
    }

    private void DetachSessionSubscriptions()
    {
        var current = _session;
        if (current is null)
        {
            return;
        }

        _session = null;
        current.OutputReceived -= OnSessionOutputReceived;
        current.Closed -= OnSessionClosed;
        current.Faulted -= OnSessionFaulted;
    }

    private void OnSessionOutputReceived(object? sender, string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return;
        }

        _receivedChunks++;
        _receivedChars += data.Length;
        if (!_firstPayloadLogged)
        {
            _firstPayloadLogged = true;
            RuntimeLogger.Info(
                "terminal-ui",
                $"First payload received. tab_id={_tabIdForLogs}, session={_session?.SessionId ?? "<none>"}, chars={data.Length}, preview={SanitizeSnippet(data, 220)}");
        }

        _outputQueue.Enqueue(data);
        ScheduleOutputFlush();
    }

    private void OnSessionClosed(object? sender, string reason)
    {
        if (sender is ISshTerminalSession session && ReferenceEquals(_session, session))
        {
            DetachSessionSubscriptions();
            var tab = ResolveTab();
            if (tab is not null)
            {
                _sessionRegistryService.TryDetach(tab.Id, out _);
            }
        }

        RuntimeLogger.Warn(
            "terminal-ui",
            $"Terminal session closed. tab_id={_tabIdForLogs}, reason={reason}, chunks={_receivedChunks}, chars={_receivedChars}");
        Dispatcher.UIThread.Post(() =>
        {
            if (!_isLoaded)
            {
                return;
            }

            SetState(SessionState.Disconnected, reason);
        });
    }

    private void OnSessionFaulted(object? sender, Exception exception)
    {
        if (sender is ISshTerminalSession session && ReferenceEquals(_session, session))
        {
            DetachSessionSubscriptions();
            var tab = ResolveTab();
            if (tab is not null)
            {
                _sessionRegistryService.TryDetach(tab.Id, out _);
            }
        }

        RuntimeLogger.Error("terminal-ui", "Terminal session faulted.", exception);
        Dispatcher.UIThread.Post(() =>
        {
            if (!_isLoaded)
            {
                return;
            }

            SetState(SessionState.Faulted, $"Terminal error: {exception.Message}");
        });
    }

    private async Task SendInputToSessionAsync(string input, string source)
    {
        var session = _session;
        if (session is null || !session.IsConnected || string.IsNullOrEmpty(input))
        {
            var now = DateTimeOffset.UtcNow;
            if ((now - _lastInputDropLogAt) > TimeSpan.FromSeconds(2))
            {
                _lastInputDropLogAt = now;
                RuntimeLogger.Warn(
                    "terminal-io",
                    $"Input skipped because session is not connected. tab_id={_tabIdForLogs}, source={source}, has_session={(session is not null).ToString().ToLowerInvariant()}");
            }

            return;
        }

        try
        {
            await session.SendAsync(input).ConfigureAwait(false);
            RuntimeLogger.Info(
                "terminal-io",
                $"Input sent. tab_id={_tabIdForLogs}, session={session.SessionId}, source={source}, chars={input.Length}");
        }
        catch (Exception ex)
        {
            RuntimeLogger.Error(
                "terminal-io",
                $"Input send failed. session={session.SessionId}, source={source}",
                ex);
        }
    }

    private void OnTerminalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = e;
        if (!TerminalHost.IsFocused)
        {
            TerminalHost.Focus();
        }
    }

    private async void OnTerminalKeyDown(object? sender, KeyEventArgs e)
    {
        if (!_isLoaded)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
            e.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (e.Key == Key.C)
            {
                e.Handled = true;
                await CopySelectionAsync("shortcut-copy");
                return;
            }

            if (e.Key == Key.V)
            {
                e.Handled = true;
                await PasteClipboardAsync("shortcut-paste");
                return;
            }
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.Insert)
        {
            e.Handled = true;
            await CopySelectionAsync("shortcut-copy");
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.Insert)
        {
            e.Handled = true;
            await PasteClipboardAsync("shortcut-paste");
            return;
        }

        var sequence = MapSpecialKeyToAnsi(e.Key, e.KeyModifiers);
        if (sequence is null)
        {
            return;
        }

        e.Handled = true;
        await SendInputToSessionAsync(sequence, $"key:{e.Key}");
    }

    private void OnTerminalTextInput(object? sender, TextInputEventArgs e)
    {
        if (!_isLoaded || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        e.Handled = true;
        _ = SendInputToSessionAsync(e.Text, "text-input");
    }

    private void OnTerminalSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        _resizeDebounceTimer.Stop();
        _resizeDebounceTimer.Start();
    }

    private void OnResizeDebounceTick(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        _resizeDebounceTimer.Stop();
        _ = SendResizeAsync();
    }

    private async Task SendResizeAsync()
    {
        var session = _session;
        if (session is null || !session.IsConnected)
        {
            return;
        }

        var cols = Math.Max(2, TerminalHost.Terminal.Cols);
        var rows = Math.Max(2, TerminalHost.Terminal.Rows);
        if (cols == _lastColumns && rows == _lastRows)
        {
            return;
        }

        _lastColumns = cols;
        _lastRows = rows;

        var widthPixels = (uint)Math.Max(1, (int)Math.Ceiling(TerminalHost.Bounds.Width));
        var heightPixels = (uint)Math.Max(1, (int)Math.Ceiling(TerminalHost.Bounds.Height));

        try
        {
            await session
                .ResizeAsync((uint)cols, (uint)rows, widthPixels, heightPixels)
                .ConfigureAwait(false);
            RuntimeLogger.Info(
                "terminal-resize",
                $"Resize sent. tab_id={_tabIdForLogs}, session={session.SessionId}, cols={cols}, rows={rows}, width={widthPixels}, height={heightPixels}");
        }
        catch (Exception ex)
        {
            RuntimeLogger.Error("terminal-resize", $"Resize failed. session={session.SessionId}", ex);
        }
    }

    private async Task CopySelectionAsync(string source)
    {
        try
        {
            var text = TerminalHost.Terminal.Selection.GetSelectionText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                return;
            }

            await clipboard.SetTextAsync(text);
            RuntimeLogger.Info("terminal-ui", $"Copied selection. source={source}, chars={text.Length}");
        }
        catch (Exception ex)
        {
            RuntimeLogger.Error("terminal-ui", "Copy failed.", ex);
        }
    }

    private async Task PasteClipboardAsync(string source)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                return;
            }

#pragma warning disable CS0618
            var text = await clipboard.GetTextAsync();
#pragma warning restore CS0618
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (TerminalHost.Terminal.BracketedPasteMode)
            {
                text = $"\u001b[200~{text}\u001b[201~";
            }

            await SendInputToSessionAsync(text, source);
        }
        catch (Exception ex)
        {
            RuntimeLogger.Error("terminal-ui", "Paste failed.", ex);
        }
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        await CopySelectionAsync("context-copy");
    }

    private async void OnPasteClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        await PasteClipboardAsync("context-paste");
    }

    private async void OnClearClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        TerminalHost.Terminal.Write("\u001b[2J\u001b[H");
        await SendInputToSessionAsync("\u000C", "context-clear");
    }

    private async void OnReconnectClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        await BeginConnectAsync(reconnect: true);
    }

    private void OnQuickStartSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        if (_hostViewModel is null)
        {
            return;
        }

        var searchText = QuickStartSearchBox.Text ?? string.Empty;
        if (string.Equals(_hostViewModel.QuickStartSearchText, searchText, StringComparison.Ordinal))
        {
            return;
        }

        _hostViewModel.QuickStartSearchText = searchText;
    }

    private void OnQuickStartConnectionClick(object? sender, RoutedEventArgs e)
    {
        _ = e;
        if (sender is not Button { DataContext: QuickStartRecentConnection recent } ||
            _hostViewModel is null)
        {
            return;
        }

        _hostViewModel.OpenQuickStartConnectionCommand.Execute(recent);
    }

    private void OnQuickStartBrowseHostsClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _hostViewModel?.LocateHostFromQuickStartCommand.Execute(null);
    }

    private void OnQuickStartNewTabClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        _hostViewModel?.CreateWorkspaceTabCommand.Execute(null);
    }

    private void RefreshQuickStartBindings()
    {
        if (!_isLoaded)
        {
            return;
        }

        QuickStartRecentItems.ItemsSource = _hostViewModel?.FilteredQuickStartRecentConnections;

        var expectedSearchText = _hostViewModel?.QuickStartSearchText ?? string.Empty;
        if (!string.Equals(QuickStartSearchBox.Text, expectedSearchText, StringComparison.Ordinal))
        {
            QuickStartSearchBox.Text = expectedSearchText;
        }

        QuickStartEmptyState.IsVisible = _hostViewModel?.IsQuickStartRecentConnectionsEmpty ?? true;
    }

    private void SetState(SessionState state, string message)
    {
        var tab = ResolveTab();
        CaptureTabForLogs(tab);
        if (tab?.ConnectionConfig is null)
        {
            ShowQuickStart(tab);
            return;
        }

        QuickStartOverlay.IsVisible = false;
        if (tab is not null)
        {
            tab.SessionState = state;
            tab.SessionStatusMessage = message;
            tab.PlaceholderText = message;
        }

        RuntimeLogger.Info(
            "terminal-state",
            $"State updated. tab_id={_tabIdForLogs}, state={state}, message={SanitizeSnippet(message, 220)}");

        switch (state)
        {
            case SessionState.Connecting:
                ConnectingOverlay.IsVisible = true;
                ConnectingRing.IsActive = true;
                ConnectingText.Text = message;
                SessionInfoBar.IsOpen = false;
                break;
            case SessionState.Connected:
                ConnectingOverlay.IsVisible = false;
                ConnectingRing.IsActive = false;
                SessionInfoBar.IsOpen = false;
                _hostViewModel?.MarkConnectionAsRecentlyUsed(tab.ConnectionConfig);
                break;
            case SessionState.Disconnected:
                ConnectingOverlay.IsVisible = false;
                ConnectingRing.IsActive = false;
                SessionInfoBar.Title = "Disconnected";
                SessionInfoBar.Message = message;
                SessionInfoBar.Severity = InfoBarSeverity.Warning;
                SessionInfoBar.IsOpen = true;
                break;
            case SessionState.Faulted:
                ConnectingOverlay.IsVisible = false;
                ConnectingRing.IsActive = false;
                SessionInfoBar.Title = "Terminal Error";
                SessionInfoBar.Message = message;
                SessionInfoBar.Severity = InfoBarSeverity.Error;
                SessionInfoBar.IsOpen = true;
                break;
        }
    }

    private void ShowQuickStart(WorkspaceTabItemViewModel? tab)
    {
        CaptureTabForLogs(tab);
        if (tab is not null)
        {
            tab.SessionState = SessionState.Disconnected;
            tab.SessionStatusMessage = "Ready";
            if (string.IsNullOrWhiteSpace(tab.PlaceholderText))
            {
                tab.PlaceholderText = "双击左侧 Hosts 资产打开终端会话";
            }
        }

        RefreshQuickStartBindings();
        QuickStartText.Text = _hostViewModel?.HasQuickStartRecentConnections == true
            ? "选择最近连接即可快速进入 SSH 终端。"
            : "暂无最近连接，双击左侧 Hosts 资产打开 SSH 会话。";
        QuickStartOverlay.IsVisible = true;
        ConnectingOverlay.IsVisible = false;
        ConnectingRing.IsActive = false;
        SessionInfoBar.IsOpen = false;
    }

    private void ScheduleOutputFlush()
    {
        if (Interlocked.Exchange(ref _flushScheduled, 1) == 1)
        {
            return;
        }

        Dispatcher.UIThread.Post(FlushOutputQueue, DispatcherPriority.Background);
    }

    private void FlushOutputQueue()
    {
        try
        {
            if (!_isLoaded)
            {
                return;
            }

            var sb = new StringBuilder(4096);
            while (_outputQueue.TryDequeue(out var chunk))
            {
                sb.Append(chunk);
                if (sb.Length >= 65536)
                {
                    break;
                }
            }

            if (sb.Length > 0)
            {
                TerminalHost.Terminal.Write(sb.ToString());
            }
        }
        finally
        {
            Interlocked.Exchange(ref _flushScheduled, 0);
            if (!_outputQueue.IsEmpty)
            {
                ScheduleOutputFlush();
            }
        }
    }

    private static string? MapSpecialKeyToAnsi(Key key, KeyModifiers modifiers)
    {
        var ctrl = modifiers.HasFlag(KeyModifiers.Control);
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        var alt = modifiers.HasFlag(KeyModifiers.Alt);

        string? sequence = null;
        if (ctrl && key is >= Key.A and <= Key.Z)
        {
            var code = (char)((int)key - (int)Key.A + 1);
            sequence = code.ToString();
        }
        else if (ctrl && key == Key.Space)
        {
            sequence = "\0";
        }
        else
        {
            sequence = key switch
            {
                Key.Enter => "\r",
                Key.Back => "\u007f",
                Key.Tab => shift ? "\u001b[Z" : "\t",
                Key.Escape => "\u001b",
                Key.Up => "\u001b[A",
                Key.Down => "\u001b[B",
                Key.Right => "\u001b[C",
                Key.Left => "\u001b[D",
                Key.Home => "\u001b[H",
                Key.End => "\u001b[F",
                Key.Insert => "\u001b[2~",
                Key.Delete => "\u001b[3~",
                Key.PageUp => "\u001b[5~",
                Key.PageDown => "\u001b[6~",
                Key.F1 => "\u001bOP",
                Key.F2 => "\u001bOQ",
                Key.F3 => "\u001bOR",
                Key.F4 => "\u001bOS",
                Key.F5 => "\u001b[15~",
                Key.F6 => "\u001b[17~",
                Key.F7 => "\u001b[18~",
                Key.F8 => "\u001b[19~",
                Key.F9 => "\u001b[20~",
                Key.F10 => "\u001b[21~",
                Key.F11 => "\u001b[23~",
                Key.F12 => "\u001b[24~",
                _ => null,
            };
        }

        if (sequence is not null && alt)
        {
            sequence = $"\u001b{sequence}";
        }

        return sequence;
    }

    private static string SanitizeSnippet(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var normalized = value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Trim();

        if (normalized.Length <= maxChars)
        {
            return normalized;
        }

        return $"{normalized[..maxChars]}...";
    }

    private static ISshConnectionService ResolveSshConnectionService()
    {
        if (Application.Current is App app && app.Services is not null)
        {
            return app.Services.GetRequiredService<ISshConnectionService>();
        }

        return new MockSshConnectionService();
    }

    private static ISessionRegistryService ResolveSessionRegistryService(ISshConnectionService sshConnectionService)
    {
        if (Application.Current is App app && app.Services is not null)
        {
            return app.Services.GetRequiredService<ISessionRegistryService>();
        }

        return new SessionRegistryService(sshConnectionService);
    }
}
