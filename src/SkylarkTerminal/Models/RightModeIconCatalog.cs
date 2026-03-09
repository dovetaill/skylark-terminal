namespace SkylarkTerminal.Models;

public static class RightModeIconCatalog
{
    public static string Resolve(RightModeIconKey key) => key switch
    {
        RightModeIconKey.Snippets => "\uE8D2",
        RightModeIconKey.History => "\uE81C",
        RightModeIconKey.RemoteFiles => "\uF0E8",
        _ => "\uE10C",
    };
}
