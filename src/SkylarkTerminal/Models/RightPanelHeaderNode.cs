namespace SkylarkTerminal.Models;

public abstract record RightPanelHeaderNode;

public sealed record ActionStripRightPanelHeader : RightPanelHeaderNode;

public sealed record SftpToolbarRightPanelHeader : RightPanelHeaderNode;
