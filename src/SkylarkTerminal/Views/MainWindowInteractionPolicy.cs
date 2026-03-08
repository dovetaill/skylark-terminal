using Avalonia.Controls;
using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;
using System;

namespace SkylarkTerminal.Views;

public static class MainWindowInteractionPolicy
{
    public static bool ShouldCloseAssetsSearchOnPointerPressed(
        bool isAssetsSearchOpen,
        string? assetsSearchText,
        bool isLeftButtonPressed,
        bool isPointerInsideSearchBox,
        bool isPointerInsideSearchToggleButton)
    {
        if (!isAssetsSearchOpen || !string.IsNullOrWhiteSpace(assetsSearchText))
        {
            return false;
        }

        if (!isLeftButtonPressed)
        {
            return false;
        }

        return !isPointerInsideSearchBox && !isPointerInsideSearchToggleButton;
    }

    public static bool TryEnsureFlatLocateTargetVisible(
        MainWindowViewModel? viewModel,
        ListBox? listBox,
        Action<ListBox, AssetNode> scrollIntoView)
    {
        if (viewModel is null ||
            !viewModel.IsFlatViewMode ||
            viewModel.PendingQuickStartLocateTarget is not AssetNode targetNode)
        {
            return false;
        }

        if (listBox is null || !listBox.IsVisible)
        {
            return false;
        }

        if (listBox.SelectedItems is not null)
        {
            listBox.SelectedItems.Clear();
            listBox.SelectedItems.Add(targetNode);
        }

        scrollIntoView(listBox, targetNode);
        return true;
    }
}
