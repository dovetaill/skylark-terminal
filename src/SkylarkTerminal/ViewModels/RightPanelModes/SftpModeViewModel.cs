using SkylarkTerminal.Models;

namespace SkylarkTerminal.ViewModels.RightPanelModes;

public sealed class SftpModeViewModel : IRightPanelModeViewModel
{
    public RightToolsViewKind Kind => RightToolsViewKind.Sftp;

    public string Title => "SFTP";

    public string Glyph => "\uE8B7";

    public RightToolsContentNode ContentNode { get; } = new SftpRightToolsContent();
}
