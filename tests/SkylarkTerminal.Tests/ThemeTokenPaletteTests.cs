using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SkylarkTerminal.Tests;

public class ThemeTokenPaletteTests
{
    [Fact]
    public void ApplyShellPalette_LightTransparentMode_ShouldUseLightSemiTransparentTerminalBackground()
    {
        var code = ReadMainWindowCodeBehind();
        var branch = Regex.Match(
            code,
            @"\(false,\s*true\)\s*=>\s*\((?<body>.*?)\),\s*\(true,\s*false\)",
            RegexOptions.Singleline);

        Assert.True(branch.Success, "Unable to find `(false, true)` palette branch.");
        var body = branch.Groups["body"].Value;
        Assert.Contains("\"#CCFFFFFF\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"#CC10141A\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyShellPalette_LightOpaqueMode_ShouldUseWhiteTerminalBackground()
    {
        var code = ReadMainWindowCodeBehind();
        var branch = Regex.Match(
            code,
            @"_\s*=>\s*\((?<body>.*?)\),\s*\};",
            RegexOptions.Singleline);

        Assert.True(branch.Success, "Unable to find light opaque (`_ =>`) palette branch.");
        var body = branch.Groups["body"].Value;
        Assert.Contains("\"#FFFFFFFF\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("\"#FF10141A\"", body, StringComparison.Ordinal);
    }

    [Fact]
    public void SshTerminalPane_ShouldConsumeTerminalTokens_ForTerminalAndOverlays()
    {
        var xaml = ReadSshTerminalPaneXaml();

        Assert.Contains("Background=\"{DynamicResource Terminal.Background}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Foreground=\"{DynamicResource Terminal.Foreground.Primary}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource Terminal.Overlay.Background}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource Terminal.Overlay.PanelBackground}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Background=\"{DynamicResource Terminal.Overlay.CardBackground}\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellTerminalBrush", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellTerminalPrimaryTextBrush", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("ShellTerminalAccentTextBrush", xaml, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})", xaml);
    }

    private static string ReadMainWindowCodeBehind()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var filePath = Path.Combine(repoRoot, "src", "SkylarkTerminal", "Views", "MainWindow.axaml.cs");
        Assert.True(File.Exists(filePath), $"MainWindow code-behind not found at {filePath}");
        return File.ReadAllText(filePath);
    }

    private static string ReadSshTerminalPaneXaml()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var filePath = Path.Combine(repoRoot, "src", "SkylarkTerminal", "Views", "SshTerminalPane.axaml");
        Assert.True(File.Exists(filePath), $"SshTerminalPane axaml not found at {filePath}");
        return File.ReadAllText(filePath);
    }
}
