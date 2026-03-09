using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkylarkTerminal.ViewModels;
using SkylarkTerminal.ViewModels.RightPanelModes;

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

        vm.ExpandAddressEditorCommand.Execute(null);
        Dispatcher.UIThread.Post(() =>
        {
            AddressEditor?.Focus();
            AddressEditor?.SelectAll();
        }, DispatcherPriority.Background);
    }

    private void OnAddressEditorLostFocus(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (!TryGetSftpModeViewModel(out var vm) || !vm.IsAddressEditorExpanded)
        {
            return;
        }

        vm.CollapseAddressEditorCommand.Execute(null);
    }

    private void OnAddressEditorKeyDown(object? sender, KeyEventArgs e)
    {
        _ = sender;
        if (e.Key != Key.Escape || !TryGetSftpModeViewModel(out var vm))
        {
            return;
        }

        vm.CollapseAddressEditorCommand.Execute(null);
        e.Handled = true;
        Dispatcher.UIThread.Post(() => AddressChipButton?.Focus(), DispatcherPriority.Background);
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
