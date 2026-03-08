using SkylarkTerminal.Models;
using System.Collections.Generic;

namespace SkylarkTerminal.ViewModels.RightPanelModes;

public sealed class SnippetsModeViewModel : IRightPanelModeViewModel
{
    public SnippetsModeViewModel(IReadOnlyList<ModeActionDescriptor>? actions = null)
    {
        Actions = actions ?? [];
    }

    public RightToolsViewKind Kind => RightToolsViewKind.Snippets;

    public string Title => "Snippets";

    public string Glyph => "\uE8D2";

    public RightToolsContentNode ContentNode { get; } = new SnippetsRightToolsContent();

    public IReadOnlyList<ModeActionDescriptor> Actions { get; }
}
