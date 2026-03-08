using SkylarkTerminal.Models;

namespace SkylarkTerminal.ViewModels.RightPanelModes;

public sealed class SnippetsModeViewModel : IRightPanelModeViewModel
{
    public RightToolsViewKind Kind => RightToolsViewKind.Snippets;

    public string Title => "Snippets";

    public string Glyph => "\uE8D2";

    public RightToolsContentNode ContentNode { get; } = new SnippetsRightToolsContent();
}
