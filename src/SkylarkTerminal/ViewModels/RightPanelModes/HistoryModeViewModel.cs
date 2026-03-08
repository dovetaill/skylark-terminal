using SkylarkTerminal.Models;

namespace SkylarkTerminal.ViewModels.RightPanelModes;

public sealed class HistoryModeViewModel : IRightPanelModeViewModel
{
    public RightToolsViewKind Kind => RightToolsViewKind.History;

    public string Title => "History";

    public string Glyph => "\uE81C";

    public RightToolsContentNode ContentNode { get; } = new HistoryRightToolsContent();
}
