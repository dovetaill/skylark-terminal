using Avalonia.Controls;
using Avalonia.Input;

namespace SkylarkTerminal.Views;

public partial class TopStatusBarView : UserControl
{
    public TopStatusBarView()
    {
        InitializeComponent();
    }

    private void OnDragRegionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || e.ClickCount > 1)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is Window window)
        {
            window.BeginMoveDrag(e);
            e.Handled = true;
        }
    }

    private void OnDragRegionDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window window || !window.CanMaximize)
        {
            return;
        }

        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
        e.Handled = true;
    }
}
