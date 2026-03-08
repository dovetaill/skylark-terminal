using SkylarkTerminal.Models;
using System.Collections.Generic;

namespace SkylarkTerminal.ViewModels.RightPanelModes;

public sealed class HistoryModeViewModel : IRightPanelModeViewModel
{
    public HistoryModeViewModel(IReadOnlyList<ModeActionDescriptor>? actions = null)
    {
        Actions = actions ?? [];
    }

    public RightToolsViewKind Kind => RightToolsViewKind.History;

    public string Title => "History";

    public string Glyph => "\uE81C";

    public RightToolsContentNode ContentNode { get; } = new HistoryRightToolsContent();

    public IReadOnlyList<ModeActionDescriptor> Actions { get; }
}
