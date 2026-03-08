using Iciclecreek.Avalonia.Terminal.Fork;
using System.Collections;
using System.Reflection;

namespace SkylarkTerminal.Tests;

public class RowStripedTerminalViewTests
{
    [Fact]
    public void Render_Order_ShouldApplyThemeThenStripeThenBaseRender()
    {
        var source = ReadRowStripedTerminalViewSource();
        var applyIndex = source.IndexOf("ApplyThemeAndFallbackColoring();", StringComparison.Ordinal);
        var stripeIndex = source.IndexOf("RenderRowStripes(context);", StringComparison.Ordinal);
        var baseIndex = source.IndexOf("base.Render(context);", StringComparison.Ordinal);

        Assert.True(applyIndex >= 0, "Missing ApplyThemeAndFallbackColoring call.");
        Assert.True(stripeIndex >= 0, "Missing RenderRowStripes call.");
        Assert.True(baseIndex >= 0, "Missing base.Render call.");
        Assert.True(applyIndex < stripeIndex, "Theme/fallback should run before stripe render.");
        Assert.True(stripeIndex < baseIndex, "Stripe render should run before base.Render.");
    }

    [Fact]
    public void RenderRowStripes_MissingStripeBrushes_ShouldReturnSafely()
    {
        var source = ReadRowStripedTerminalViewSource();

        Assert.Contains("if (evenBrush is null || oddBrush is null)", source, StringComparison.Ordinal);
        Assert.Contains("return;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SyncAnsiThemePalette_ShouldMapTerminalThemeAndAssignOptionsTheme()
    {
        var source = ReadRowStripedTerminalViewSource();

        Assert.Contains("theme.Background = background;", source, StringComparison.Ordinal);
        Assert.Contains("theme.Foreground = foreground;", source, StringComparison.Ordinal);
        Assert.Contains("theme.Selection = selection;", source, StringComparison.Ordinal);
        Assert.Contains("theme.Cursor = cursor;", source, StringComparison.Ordinal);
        Assert.Contains("theme.Red = error;", source, StringComparison.Ordinal);
        Assert.Contains("theme.Green = success;", source, StringComparison.Ordinal);
        Assert.Contains("theme.Yellow = warn;", source, StringComparison.Ordinal);
        Assert.Contains("theme.Cyan = path;", source, StringComparison.Ordinal);
        Assert.Contains("options.Theme = theme;", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ShouldApplyFallback_ShouldOnlyApplyForDefaultForegroundCells()
    {
        var source = ReadRowStripedTerminalViewSource();

        Assert.Contains("attr.GetFgColorMode() == PaletteColorMode", source, StringComparison.Ordinal);
        Assert.Contains("attr.GetFgColor() == DefaultFgColor", source, StringComparison.Ordinal);
        Assert.Contains("attr.GetBgColor() == DefaultBgColor", source, StringComparison.Ordinal);
        Assert.Contains("!attr.IsInverse()", source, StringComparison.Ordinal);
        Assert.Contains("!attr.IsInvisible()", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildFallbackTokenSpans_L1Rules_ShouldMatchExpectedTokenClasses()
    {
        const string text = "error warning success /var/log host.example.com 1234";
        var spans = InvokeBuildFallbackTokenSpans(text);

        Assert.True(HasSpan(text, spans, "error", 9), "Expected error token to be bright red (9).");
        Assert.True(HasSpan(text, spans, "warning", 11), "Expected warning token to be bright yellow (11).");
        Assert.True(HasSpan(text, spans, "success", 10), "Expected success token to be bright green (10).");
        Assert.True(HasSpan(text, spans, "/var/log", 14), "Expected path token to be bright cyan (14).");
        Assert.True(HasSpan(text, spans, "host.example.com", 6), "Expected host token to be cyan (6).");
        Assert.True(HasSpan(text, spans, "1234", 11), "Expected number token to be bright yellow (11).");
    }

    [Fact]
    public void BuildFallbackTokenSpans_L2ShellRules_ShouldMatchShellAwareTokens()
    {
        const string text = "ops@node:~$ echo $HOME --all \"abc\" | grep foo > out.txt";
        var spans = InvokeBuildFallbackTokenSpans(text);

        Assert.True(HasSpan(text, spans, "echo", 12), "Expected command token to be bright blue (12).");
        Assert.True(HasSpan(text, spans, "$HOME", 13), "Expected variable token to be bright magenta (13).");
        Assert.True(HasSpan(text, spans, "--all", 12), "Expected flag token to be bright blue (12).");
        Assert.True(HasSpan(text, spans, "\"abc\"", 10), "Expected quoted token to be bright green (10).");
        Assert.True(HasSpan(text, spans, "|", 8), "Expected pipe token to be bright black (8).");
        Assert.True(HasSpan(text, spans, ">", 8), "Expected redirect token to be bright black (8).");
    }

    private static string ReadRowStripedTerminalViewSource()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var filePath = Path.Combine(
            repoRoot,
            "src",
            "ThirdParty",
            "Iciclecreek.Avalonia.Terminal.Fork",
            "RowStripedTerminalView.cs");
        Assert.True(File.Exists(filePath), $"RowStripedTerminalView source not found at {filePath}");
        return File.ReadAllText(filePath);
    }

    private static List<TokenSpanInfo> InvokeBuildFallbackTokenSpans(string text)
    {
        var method = typeof(RowStripedTerminalView).GetMethod(
            "BuildFallbackTokenSpans",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var result = method!.Invoke(null, [text]) as IEnumerable;
        Assert.NotNull(result);

        var spans = new List<TokenSpanInfo>();
        foreach (var item in result!)
        {
            Assert.NotNull(item);
            var type = item!.GetType();
            var start = ReadIntProperty(type, item, "Start");
            var length = ReadIntProperty(type, item, "Length");
            var colorIndex = ReadIntProperty(type, item, "ColorIndex");
            spans.Add(new TokenSpanInfo(start, length, colorIndex));
        }

        return spans;
    }

    private static int ReadIntProperty(Type type, object instance, string propertyName)
    {
        var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(property);
        var value = property!.GetValue(instance);
        Assert.NotNull(value);
        return (int)value!;
    }

    private static bool HasSpan(string source, IEnumerable<TokenSpanInfo> spans, string token, int expectedColorIndex)
    {
        var start = source.IndexOf(token, StringComparison.Ordinal);
        if (start < 0)
        {
            return false;
        }

        var endExclusive = start + token.Length;
        return spans.Any(span =>
            span.ColorIndex == expectedColorIndex &&
            span.Start <= start &&
            span.EndExclusive >= endExclusive);
    }

    private readonly record struct TokenSpanInfo(int Start, int Length, int ColorIndex)
    {
        public int EndExclusive => Start + Length;
    }
}
