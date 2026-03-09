using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FluentAvalonia.UI.Controls;
using SkylarkTerminal.ViewModels;
using SkylarkTerminal.ViewModels.RightPanelModes;
using System;
using System.Linq;

namespace SkylarkTerminal.Views.RightHeaders;

public partial class SftpToolbarHeaderView : UserControl
{
    public SftpToolbarHeaderView()
    {
        InitializeComponent();
    }

    private void OnAddressChipClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (!TryGetSftpModeViewModel(out var vm))
        {
            return;
        }

        vm.OpenAddressOverlayCommand.Execute(null);
        Dispatcher.UIThread.Post(() =>
        {
            AddressOverlayTextBox?.Focus();
            AddressOverlayTextBox?.SelectAll();
        }, DispatcherPriority.Background);
    }

    private void OnSearchButtonClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (!TryGetSftpModeViewModel(out var vm))
        {
            return;
        }

        vm.OpenSearchOverlayCommand.Execute(null);
        Dispatcher.UIThread.Post(() =>
        {
            SearchOverlayTextBox?.Focus();
            SearchOverlayTextBox?.SelectAll();
        }, DispatcherPriority.Background);
    }

    private void OnOverlayEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (!TryGetSftpModeViewModel(out var vm) || !vm.IsHeaderOverlayVisible)
        {
            return;
        }

        vm.CloseHeaderOverlayCommand.Execute(null);
    }

    private void OnOverlayEditorKeyDown(object? sender, KeyEventArgs e)
    {
        _ = sender;
        if (e.Key != Key.Escape || !TryGetSftpModeViewModel(out var vm))
        {
            return;
        }

        vm.CloseHeaderOverlayCommand.Execute(null);
        e.Handled = true;
        Dispatcher.UIThread.Post(() => AddressChipButton?.Focus(), DispatcherPriority.Background);
    }

    private void OnHistoryFlyoutOpening(object? sender, EventArgs e)
    {
        _ = e;
        if (sender is not FAMenuFlyout historyFlyout || !TryGetSftpModeViewModel(out var vm))
        {
            return;
        }

        historyFlyout.Items.Clear();

        foreach (var path in vm.RecentPaths.Take(8))
        {
            historyFlyout.Items.Add(new MenuFlyoutItem
            {
                Text = path,
                Command = vm.NavigateHistoryPathCommand,
                CommandParameter = path,
                IconSource = new FontIconSource
                {
                    FontFamily = "Segoe Fluent Icons, Segoe MDL2 Assets",
                    Glyph = "\uE81C",
                },
            });
        }
    }

    private bool TryGetSftpModeViewModel(out SftpModeViewModel vm)
    {
        vm = null!;

        var host = this.FindAncestorOfType<RightSidebarHostView>();
        if (host?.DataContext is not MainWindowViewModel { ActiveRightMode: SftpModeViewModel sftpMode })
        {
            return false;
        }

        vm = sftpMode;
        return true;
    }
}
