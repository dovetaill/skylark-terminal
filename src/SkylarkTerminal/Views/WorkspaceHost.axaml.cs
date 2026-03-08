using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SkylarkTerminal.Models;
using SkylarkTerminal.ViewModels;
using System;
using System.Collections.Specialized;
using System.ComponentModel;

namespace SkylarkTerminal.Views;

public partial class WorkspaceHost : UserControl
{
    private MainWindowViewModel? _viewModel;

    public WorkspaceHost()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        _ = sender;
        _ = e;
        AttachViewModel(DataContext as MainWindowViewModel);
    }

    private void AttachViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_viewModel, viewModel))
        {
            return;
        }

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.WorkspacePanes.CollectionChanged -= OnWorkspacePanesCollectionChanged;
        }

        _viewModel = viewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.WorkspacePanes.CollectionChanged += OnWorkspacePanesCollectionChanged;
        }

        RenderWorkspace();
    }

    private void OnWorkspacePanesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;
        _ = e;
        RenderWorkspace();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        if (string.Equals(e.PropertyName, nameof(MainWindowViewModel.WorkspaceLayoutRoot), StringComparison.Ordinal))
        {
            RenderWorkspace();
        }
    }

    private void RenderWorkspace()
    {
        if (_viewModel is null)
        {
            WorkspaceRoot.Content = null;
            return;
        }

        WorkspaceRoot.Content = BuildNode(_viewModel.WorkspaceLayoutRoot);
    }

    private Control BuildNode(WorkspaceLayoutNode node)
    {
        return node switch
        {
            PaneNode paneNode => BuildPaneNode(paneNode),
            SplitNode splitNode => BuildSplitNode(splitNode),
            _ => new Border
            {
                Background = Brushes.Transparent,
            },
        };
    }

    private Control BuildPaneNode(PaneNode paneNode)
    {
        if (_viewModel is null)
        {
            return new Border();
        }

        var pane = _viewModel.GetOrCreateWorkspacePane(paneNode.PaneId);
        return new WorkspacePaneHost
        {
            DataContext = _viewModel,
            Pane = pane,
        };
    }

    private Control BuildSplitNode(SplitNode splitNode)
    {
        var ratio = Math.Clamp(splitNode.Ratio, 0.15d, 0.85d);
        var grid = new Grid
        {
            ClipToBounds = true,
        };

        if (splitNode.Orientation == WorkspaceSplitOrientation.Horizontal)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(ratio, GridUnitType.Star),
            });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = GridLength.Auto,
            });
            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1d - ratio, GridUnitType.Star),
            });

            var first = BuildNode(splitNode.First);
            Grid.SetColumn(first, 0);
            grid.Children.Add(first);

            var splitter = new GridSplitter
            {
                Width = 8d,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch,
                ResizeDirection = GridResizeDirection.Columns,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                Background = Brushes.Transparent,
            };
            splitter.Classes.Add("ShellColumnSplitter");
            Grid.SetColumn(splitter, 1);
            grid.Children.Add(splitter);

            var second = BuildNode(splitNode.Second);
            Grid.SetColumn(second, 2);
            grid.Children.Add(second);
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(ratio, GridUnitType.Star),
            });
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto,
            });
            grid.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(1d - ratio, GridUnitType.Star),
            });

            var first = BuildNode(splitNode.First);
            Grid.SetRow(first, 0);
            grid.Children.Add(first);

            var splitter = new GridSplitter
            {
                Height = 8d,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                ResizeDirection = GridResizeDirection.Rows,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                Background = Brushes.Transparent,
            };
            splitter.Classes.Add("ShellColumnSplitter");
            Grid.SetRow(splitter, 1);
            grid.Children.Add(splitter);

            var second = BuildNode(splitNode.Second);
            Grid.SetRow(second, 2);
            grid.Children.Add(second);
        }

        return grid;
    }
}
