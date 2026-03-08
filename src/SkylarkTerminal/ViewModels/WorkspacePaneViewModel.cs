using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;

namespace SkylarkTerminal.ViewModels;

public partial class WorkspacePaneViewModel : ObservableObject
{
    public WorkspacePaneViewModel(string paneId)
    {
        if (string.IsNullOrWhiteSpace(paneId))
        {
            throw new ArgumentException("Pane id cannot be null or whitespace.", nameof(paneId));
        }

        PaneId = paneId;
    }

    public string PaneId { get; }

    public ObservableCollection<WorkspaceTabItemViewModel> Tabs { get; } = [];

    [ObservableProperty]
    private WorkspaceTabItemViewModel? selectedTab;
}
