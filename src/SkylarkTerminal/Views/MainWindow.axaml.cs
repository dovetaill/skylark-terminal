using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using SkylarkTerminal.ViewModels;

namespace SkylarkTerminal.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnTopTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (IsPointerOnInteractiveElement(e.Source as StyledElement))
        {
            return;
        }

        BeginMoveDrag(e);
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

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OnMaximizeRestoreClick(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private static bool IsPointerOnInteractiveElement(StyledElement? element)
    {
        var current = element;
        while (current is not null)
        {
            if (current is Button or TextBox or Menu or MenuItem)
            {
                return true;
            }

            if (current is Border border && border.Name == "TopTitleBar")
            {
                return false;
            }

            current = current.Parent as StyledElement;
        }

        return false;
    }
}
