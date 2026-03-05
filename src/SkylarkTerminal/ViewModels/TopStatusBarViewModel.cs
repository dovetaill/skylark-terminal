using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;

namespace SkylarkTerminal.ViewModels;

public partial class TopStatusBarViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _shell;
    private Action? _minimizeWindowAction;
    private Action? _toggleMaximizeRestoreWindowAction;
    private Action? _closeWindowAction;

    public TopStatusBarViewModel(MainWindowViewModel shell)
    {
        _shell = shell;
        _shell.PropertyChanged += OnShellPropertyChanged;
    }

    public string AppDisplayName => _shell.WindowTitle;

    public string ThemeIconGlyph => _shell.ThemeIconGlyph;

    public string ThemeToggleToolTip => _shell.ThemeToggleToolTip;

    public string SessionStatusText => "Ready";

    public string WindowMaximizeGlyph => CurrentWindowState == WindowState.Maximized
        ? "\uE923"
        : "\uE922";

    public string WindowMaximizeToolTip => CurrentWindowState == WindowState.Maximized
        ? "还原"
        : "最大化";

    [ObservableProperty]
    private WindowState currentWindowState;

    public void AttachWindowActions(
        Action minimizeWindowAction,
        Action toggleMaximizeRestoreWindowAction,
        Action closeWindowAction)
    {
        _minimizeWindowAction = minimizeWindowAction;
        _toggleMaximizeRestoreWindowAction = toggleMaximizeRestoreWindowAction;
        _closeWindowAction = closeWindowAction;
    }

    public void SetWindowState(WindowState state)
    {
        CurrentWindowState = state;
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        _shell.ToggleThemeCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleLeftSidebar()
    {
        _shell.ToggleLeftAssetsPaneCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleRightSidebar()
    {
        _shell.ToggleRightToolsPaneCommand.Execute(null);
    }

    [RelayCommand]
    private void MinimizeWindow()
    {
        _minimizeWindowAction?.Invoke();
    }

    [RelayCommand]
    private void ToggleMaximizeRestoreWindow()
    {
        _toggleMaximizeRestoreWindowAction?.Invoke();
    }

    [RelayCommand]
    private void CloseWindow()
    {
        _closeWindowAction?.Invoke();
    }

    partial void OnCurrentWindowStateChanged(WindowState value)
    {
        OnPropertyChanged(nameof(WindowMaximizeGlyph));
        OnPropertyChanged(nameof(WindowMaximizeToolTip));
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.ThemeIconGlyph))
        {
            OnPropertyChanged(nameof(ThemeIconGlyph));
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.ThemeToggleToolTip))
        {
            OnPropertyChanged(nameof(ThemeToggleToolTip));
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.WindowTitle))
        {
            OnPropertyChanged(nameof(AppDisplayName));
        }
    }
}
