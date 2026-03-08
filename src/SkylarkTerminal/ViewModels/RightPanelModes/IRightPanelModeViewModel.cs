using SkylarkTerminal.Models;
using System.Collections.Generic;

namespace SkylarkTerminal.ViewModels.RightPanelModes;

public interface IRightPanelModeViewModel
{
    RightToolsViewKind Kind { get; }

    string Title { get; }

    string Glyph { get; }

    RightToolsContentNode ContentNode { get; }

    IReadOnlyList<ModeActionDescriptor> Actions { get; }
}
