using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using SkylarkTerminal.ViewModels;
using System;

namespace SkylarkTerminal.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        vm.TopStatusBar.AttachWindowActions(
            minimizeWindowAction: () => WindowState = WindowState.Minimized,
            toggleMaximizeRestoreWindowAction: ToggleMaximizeRestoreWindow,
            closeWindowAction: Close);
        vm.TopStatusBar.SetWindowState(WindowState);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != WindowStateProperty || DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        vm.TopStatusBar.SetWindowState(change.GetNewValue<WindowState>());
    }

    private void OnWorkspaceTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (args.Item is WorkspaceTabItemViewModel tab)
        {
            vm.CloseTabCommand.Execute(tab);
        }
    }

    private void OnWorkspaceTabContextActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        var tab = menuItem.DataContext as WorkspaceTabItemViewModel;
        var action = menuItem.Tag?.ToString();

        switch (action)
        {
            case "duplicate":
                vm.DuplicateTabCommand.Execute(tab);
                break;
            case "close":
                vm.CloseTabCommand.Execute(tab);
                break;
            case "close-others":
                vm.CloseOtherTabsCommand.Execute(tab);
                break;
            case "close-right":
                vm.CloseTabsToRightCommand.Execute(tab);
                break;
            case "close-all":
                vm.CloseAllTabsCommand.Execute(null);
                break;
        }
    }

    private void ToggleMaximizeRestoreWindow()
    {
        if (!CanMaximize)
        {
            return;
        }

        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}
