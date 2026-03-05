using Avalonia;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SkylarkTerminal.ViewModels;

public partial class WorkspaceTabItemViewModel : ObservableObject
{
    private static readonly IBrush ActiveHeaderBackground = new SolidColorBrush(Color.Parse("#22000000"));
    private static readonly IBrush InactiveHeaderBackground = new SolidColorBrush(Color.Parse("#14000000"));

    public WorkspaceTabItemViewModel(
        string id,
        string header,
        string connectionLabel,
        string placeholderText,
        IBrush accentBrush)
    {
        Id = id;
        this.header = header;
        this.connectionLabel = connectionLabel;
        this.placeholderText = placeholderText;
        AccentBrush = accentBrush;
    }

    public string Id { get; }

    [ObservableProperty]
    private string header;

    [ObservableProperty]
    private string connectionLabel;

    [ObservableProperty]
    private string placeholderText;

    [ObservableProperty]
    private bool isActive;

    public IBrush AccentBrush { get; }

    public IBrush HeaderBackgroundBrush => IsActive ? ActiveHeaderBackground : InactiveHeaderBackground;

    public Thickness HeaderBottomBorderThickness => IsActive
        ? new Thickness(0, 0, 0, 2)
        : new Thickness(0);

    public FontWeight HeaderFontWeight => IsActive ? FontWeight.SemiBold : FontWeight.Normal;

    public double HeaderOpacity => IsActive ? 1d : 0.84d;

    public WorkspaceTabItemViewModel DuplicateAs(string duplicatedId)
    {
        return new WorkspaceTabItemViewModel(
            duplicatedId,
            $"{Header} Copy",
            ConnectionLabel,
            $"{PlaceholderText} (duplicated)",
            AccentBrush);
    }

    partial void OnIsActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(HeaderBackgroundBrush));
        OnPropertyChanged(nameof(HeaderBottomBorderThickness));
        OnPropertyChanged(nameof(HeaderFontWeight));
        OnPropertyChanged(nameof(HeaderOpacity));
    }
}
