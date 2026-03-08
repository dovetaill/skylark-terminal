using SkylarkTerminal.Models;
using System.Collections.Generic;

namespace SkylarkTerminal.ViewModels.RightPanelModes;

public sealed class SftpModeViewModel : IRightPanelModeViewModel
{
    public SftpModeViewModel(IReadOnlyList<ModeActionDescriptor>? actions = null)
    {
        Actions = actions ?? [];
    }

    public RightToolsViewKind Kind => RightToolsViewKind.Sftp;

    public string Title => "SFTP";

    public string Glyph => "\uE8B7";

    public RightToolsContentNode ContentNode { get; } = new SftpRightToolsContent();

    public IReadOnlyList<ModeActionDescriptor> Actions { get; }
}
