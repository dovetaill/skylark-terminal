using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using SkylarkTerminal.ViewModels.RightPanelModes;
using System;
using System.Threading.Tasks;

namespace SkylarkTerminal.Views.RightModes;

public partial class SnippetsModeView : UserControl
{
    public SnippetsModeView()
    {
        InitializeComponent();
    }

    private async void OnSnippetCardDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (!TryGetSnippetContext(sender, out var vm, out var item))
        {
            return;
        }

        await vm.PasteAsync(item);
        e.Handled = true;
    }

    private async void OnSnippetActionClick(object? sender, RoutedEventArgs e)
    {
        _ = e;
        await HandleSnippetActionAsync(sender);
    }

    private async void OnSnippetContextActionClick(object? sender, RoutedEventArgs e)
    {
        _ = e;
        await HandleSnippetActionAsync(sender);
    }

    private async Task HandleSnippetActionAsync(object? sender)
    {
        if (!TryGetSnippetContext(sender, out var vm, out var item))
        {
            return;
        }

        var action = sender switch
        {
            Button button => button.Tag?.ToString(),
            MenuFlyoutItem flyoutItem => flyoutItem.Tag?.ToString(),
            MenuItem menuItem => menuItem.Tag?.ToString(),
            _ => null,
        };

        switch (action)
        {
            case "copy":
                await CopySnippetAsync(item);
                break;
            case "run":
                await vm.RunAsync(item);
                break;
            case "paste":
                await vm.PasteAsync(item);
                break;
            case "run-all":
                await vm.RunInAllTabsAsync(item);
                break;
            case "edit":
                vm.OpenEditCommand.Execute(item);
                break;
            case "view-more":
                vm.OpenViewMoreCommand.Execute(item);
                break;
        }
    }

    private async void OnEditorSaveClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (!TryGetViewModel(out var vm))
        {
            return;
        }

        await vm.SaveDraftAsync();
    }

    private void OnEditorCancelClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (!TryGetViewModel(out var vm))
        {
            return;
        }

        vm.CancelEditCommand.Execute(null);
    }

    private async void OnEditorRemoveClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (!TryGetViewModel(out var vm))
        {
            return;
        }

        await vm.DeleteSelectedSnippetAsync();
    }

    private void OnViewMoreBackClick(object? sender, RoutedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (!TryGetViewModel(out var vm))
        {
            return;
        }

        vm.CancelEditCommand.Execute(null);
    }

    private async Task CopySnippetAsync(SnippetItem item)
    {
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null)
            {
                return;
            }

            await clipboard.SetTextAsync(item.Content);
        }
        catch (Exception ex)
        {
            RuntimeLogger.Error("snippets-ui", "Copy snippet failed.", ex);
        }
    }

    private bool TryGetSnippetContext(
        object? sender,
        out SnippetsModeViewModel vm,
        out SnippetItem item)
    {
        vm = null!;
        item = null!;

        if (!TryGetViewModel(out var snippetsVm))
        {
            return false;
        }

        var control = sender as Control;
        if (control?.DataContext is not SnippetItem snippetItem)
        {
            return false;
        }

        vm = snippetsVm;
        item = snippetItem;
        return true;
    }

    private bool TryGetViewModel(out SnippetsModeViewModel vm)
    {
        vm = null!;

        if (DataContext is not SnippetsModeViewModel snippetsVm)
        {
            return false;
        }

        vm = snippetsVm;
        return true;
    }
}
