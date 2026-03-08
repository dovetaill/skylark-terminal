using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FluentAvalonia.UI.Controls;
using SkylarkTerminal.Models;
using SkylarkTerminal.Services;
using SkylarkTerminal.ViewModels;
using System;
using System.Linq;

namespace SkylarkTerminal.Views;

public partial class WorkspacePaneHost : UserControl
{
    public static readonly StyledProperty<WorkspacePaneViewModel?> PaneProperty =
        AvaloniaProperty.Register<WorkspacePaneHost, WorkspacePaneViewModel?>(nameof(Pane));

    public WorkspacePaneHost()
    {
        InitializeComponent();
    }

    public WorkspacePaneViewModel? Pane
    {
        get => GetValue(PaneProperty);
        set => SetValue(PaneProperty, value);
    }

    private void OnWorkspaceTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        _ = sender;
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (args.Item is WorkspaceTabItemViewModel tab)
        {
            RuntimeLogger.Info("tab-close", $"Close requested. tab_id={tab.Id}, header={tab.Header}");
            vm.CloseTabCommand.Execute(tab);
        }
    }

    private void OnWorkspaceTabContextActionClick(object? sender, RoutedEventArgs e)
    {
        _ = e;
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        WorkspaceTabItemViewModel? tab = null;
        string? action = null;

        if (sender is MenuItem menuItem)
        {
            tab = menuItem.DataContext as WorkspaceTabItemViewModel;
            action = menuItem.Tag?.ToString();
        }
        else if (sender is MenuFlyoutItem flyoutItem)
        {
            tab = flyoutItem.DataContext as WorkspaceTabItemViewModel;
            action = flyoutItem.Tag?.ToString();
        }
        else
        {
            return;
        }

        switch (action)
        {
            case "duplicate":
                RuntimeLogger.Info("tab-context", $"Action duplicate. tab_id={tab?.Id ?? "<null>"}");
                vm.DuplicateTabCommand.Execute(tab);
                break;
            case "close":
                RuntimeLogger.Info("tab-context", $"Action close. tab_id={tab?.Id ?? "<null>"}");
                vm.CloseTabCommand.Execute(tab);
                break;
            case "close-others":
                RuntimeLogger.Info("tab-context", $"Action close-others. tab_id={tab?.Id ?? "<null>"}");
                vm.CloseOtherTabsCommand.Execute(tab);
                break;
            case "close-left":
                RuntimeLogger.Info("tab-context", $"Action close-left. tab_id={tab?.Id ?? "<null>"}");
                vm.CloseTabsToLeftCommand.Execute(tab);
                break;
            case "close-right":
                RuntimeLogger.Info("tab-context", $"Action close-right. tab_id={tab?.Id ?? "<null>"}");
                vm.CloseTabsToRightCommand.Execute(tab);
                break;
            case "close-all":
                RuntimeLogger.Info("tab-context", "Action close-all.");
                vm.CloseAllTabsCommand.Execute(null);
                break;
        }
    }

    private void OnWorkspaceTabSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _ = sender;
        if (DataContext is not MainWindowViewModel vm || Pane is null)
        {
            return;
        }

        vm.SelectedWorkspacePane = Pane;
        var selected = e.AddedItems.OfType<WorkspaceTabItemViewModel>().FirstOrDefault();
        if (selected is not null && !ReferenceEquals(vm.SelectedWorkspaceTab, selected))
        {
            vm.SelectedWorkspaceTab = selected;
        }
    }

    private void OnWorkspaceTabViewPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _ = sender;
        _ = e;
        if (DataContext is MainWindowViewModel vm && Pane is not null)
        {
            vm.SelectedWorkspacePane = Pane;
        }
    }

    private void OnWorkspaceTabDragStarting(TabView sender, TabViewTabDragStartingEventArgs args)
    {
        _ = sender;
        if (DataContext is not MainWindowViewModel vm || Pane is null)
        {
            return;
        }

        if (args.Item is not WorkspaceTabItemViewModel tab)
        {
            args.Cancel = true;
            return;
        }

        vm.SelectedWorkspacePane = Pane;
        vm.BeginWorkspaceDragPreview(Pane.PaneId, tab.Id, tab);
    }

    private void OnWorkspaceTabDragCompleted(TabView sender, TabViewTabDragCompletedEventArgs args)
    {
        _ = sender;
        _ = args;
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        if (vm.HasWorkspaceDragSession)
        {
            vm.CancelWorkspaceDragPreview();
        }
    }

    private void OnWorkspaceTabStripDragOver(object? sender, DragEventArgs e)
    {
        _ = sender;
        if (DataContext is not MainWindowViewModel vm || Pane is null)
        {
            return;
        }

        if (!vm.HasWorkspaceDragSession)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var direction = ResolveDropDirection(e, vm.WorkspaceMinPaneSize);
        vm.UpdateWorkspaceDragPreview(Pane.PaneId, direction);
        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void OnWorkspaceTabStripDrop(object? sender, DragEventArgs e)
    {
        _ = sender;
        if (DataContext is not MainWindowViewModel vm || Pane is null)
        {
            return;
        }

        if (!vm.HasWorkspaceDragSession)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var direction = ResolveDropDirection(e, vm.WorkspaceMinPaneSize);
        var accepted = vm.CompleteWorkspaceDragDrop(Pane.PaneId, direction);
        if (!accepted)
        {
            vm.CancelWorkspaceDragPreview();
        }

        e.DragEffects = accepted ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private WorkspaceDropDirection? ResolveDropDirection(DragEventArgs e, double minPaneSize)
    {
        var point = e.GetPosition(this);
        var width = Bounds.Width;
        var height = Bounds.Height;
        return WorkspacePaneDropPolicy.ResolveDropDirection(point, width, height, minPaneSize);
    }
}
