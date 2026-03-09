namespace SkylarkTerminal.Models;

public sealed record RightToolsModeItem(
    RightToolsViewKind Kind,
    string TitleZh,
    string TooltipZh,
    RightModeIconKey IconKey)
{
    public string Glyph => RightModeIconCatalog.Resolve(IconKey);
}
