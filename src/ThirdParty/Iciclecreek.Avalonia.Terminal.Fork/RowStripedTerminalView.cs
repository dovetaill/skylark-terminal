using Avalonia;
using Avalonia.Media;
using Iciclecreek.Terminal;
using System.Text.RegularExpressions;
using XTerm.Buffer;
using XTerm.Common;
using XTerm.Options;

namespace Iciclecreek.Avalonia.Terminal.Fork;

public class RowStripedTerminalView : TerminalView
{
    private const string EvenStripeKey = "Terminal.RowStripe.Even";
    private const string OddStripeKey = "Terminal.RowStripe.Odd";
    private const string BackgroundKey = "Terminal.Background";
    private const string ForegroundKey = "Terminal.Foreground.Primary";
    private const string MutedForegroundKey = "Terminal.Foreground.Muted";
    private const string SelectionKey = "Terminal.Selection";
    private const string CursorKey = "Terminal.Cursor";
    private const string KeywordKey = "Terminal.Syntax.Keyword";
    private const string StringKey = "Terminal.Syntax.String";
    private const string NumberKey = "Terminal.Syntax.Number";
    private const string PathKey = "Terminal.Syntax.Path";
    private const string ErrorKey = "Terminal.Syntax.Error";
    private const string WarnKey = "Terminal.Syntax.Warn";
    private const string SuccessKey = "Terminal.Syntax.Success";
    private const string CommentKey = "Terminal.Syntax.Comment";
    private const int DefaultFgColor = 256;
    private const int DefaultBgColor = 257;
    private const int PaletteColorMode = (int)ColorMode.Palette256;
    private const int AnsiBlack = 0;
    private const int AnsiRed = 1;
    private const int AnsiGreen = 2;
    private const int AnsiYellow = 3;
    private const int AnsiBlue = 4;
    private const int AnsiMagenta = 5;
    private const int AnsiCyan = 6;
    private const int AnsiWhite = 7;
    private const int AnsiBrightBlack = 8;
    private const int AnsiBrightRed = 9;
    private const int AnsiBrightGreen = 10;
    private const int AnsiBrightYellow = 11;
    private const int AnsiBrightBlue = 12;
    private const int AnsiBrightMagenta = 13;
    private const int AnsiBrightCyan = 14;
    private const int AnsiBrightWhite = 15;
    private static readonly Regex ErrorTokenRegex = new(
        @"\b(error|failed|exception|fatal|denied|forbidden|unauthorized|not found|traceback)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WarnTokenRegex = new(
        @"\b(warn|warning|deprecated|timeout|slow)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SuccessTokenRegex = new(
        @"\b(ok|success|succeeded|done|completed|connected)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PathTokenRegex = new(
        @"(?<!\S)(?:~\/|\.{1,2}\/|\/)[^\s]+|[A-Za-z]:\\[^\s]+",
        RegexOptions.Compiled);
    private static readonly Regex HostTokenRegex = new(
        @"\b(?:(?:\d{1,3}\.){3}\d{1,3}|(?:[a-z0-9-]+\.)+[a-z]{2,})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex NumberTokenRegex = new(
        @"\b\d+(?:\.\d+)?\b",
        RegexOptions.Compiled);
    private static readonly Regex ShellPromptRegex = new(
        @"^\s*(?:\[[^\]]+\]\s*)?(?:[\w\.-]+@[\w\.-]+(?::[~\/\w\.-]+)?[#$]\s+|PS [^>]*>\s+|[#$]\s+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ShellVariableRegex = new(
        @"\$[A-Za-z_][A-Za-z0-9_]*",
        RegexOptions.Compiled);
    private static readonly Regex ShellOperatorRegex = new(
        @"[|><]",
        RegexOptions.Compiled);
    private static readonly Regex QuotedSegmentRegex = new(
        "'[^']*'|\"[^\"]*\"",
        RegexOptions.Compiled);
    private static readonly Regex ShellFlagRegex = new(
        @"(?<!\S)--?[A-Za-z0-9][A-Za-z0-9-]*",
        RegexOptions.Compiled);
    private static readonly Regex CommandTokenRegex = new(
        @"^\s*(?:\[[^\]]+\]\s*)?(?:[\w\.-]+@[\w\.-]+(?::[~\/\w\.-]+)?[#$]\s+|PS [^>]*>\s+|[#$]\s+)?(?<cmd>[A-Za-z_][A-Za-z0-9_-]*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private string? _lastThemeFingerprint;

    public override void Render(DrawingContext context)
    {
        ApplyThemeAndFallbackColoring();
        RenderRowStripes(context);
        base.Render(context);
    }

    private void ApplyThemeAndFallbackColoring()
    {
        if (Terminal is null)
        {
            return;
        }

        SyncAnsiThemePalette();
        ApplyFallbackHighlighting(Terminal.Buffer);
    }

    private void SyncAnsiThemePalette()
    {
        if (Terminal is null)
        {
            return;
        }

        var background = ResolveHexColor(BackgroundKey);
        var foreground = ResolveHexColor(ForegroundKey);
        var selection = ResolveHexColor(SelectionKey);
        var cursor = ResolveHexColor(CursorKey);
        var error = ResolveHexColor(ErrorKey);
        var success = ResolveHexColor(SuccessKey);
        var warn = ResolveHexColor(WarnKey);
        var keyword = ResolveHexColor(KeywordKey);
        var path = ResolveHexColor(PathKey);
        var number = ResolveHexColor(NumberKey);
        var comment = ResolveHexColor(CommentKey);
        var muted = ResolveHexColor(MutedForegroundKey);

        if (background is null ||
            foreground is null ||
            selection is null ||
            cursor is null ||
            error is null ||
            success is null ||
            warn is null ||
            keyword is null ||
            path is null ||
            number is null ||
            comment is null ||
            muted is null)
        {
            return;
        }

        var fingerprint = string.Join(
            "|",
            background,
            foreground,
            selection,
            cursor,
            error,
            success,
            warn,
            keyword,
            path,
            number,
            comment,
            muted);

        if (string.Equals(_lastThemeFingerprint, fingerprint, StringComparison.Ordinal))
        {
            return;
        }

        var options = Terminal.Options;
        var theme = options.Theme ?? new ThemeOptions();
        theme.Background = background;
        theme.Foreground = foreground;
        theme.Selection = selection;
        theme.SelectionInactive = selection;
        theme.Cursor = cursor;
        theme.CursorAccent = background;
        theme.Black = comment;
        theme.Red = error;
        theme.Green = success;
        theme.Yellow = warn;
        theme.Blue = keyword;
        theme.Magenta = keyword;
        theme.Cyan = path;
        theme.White = foreground;
        theme.BrightBlack = muted;
        theme.BrightRed = error;
        theme.BrightGreen = success;
        theme.BrightYellow = number;
        theme.BrightBlue = keyword;
        theme.BrightMagenta = keyword;
        theme.BrightCyan = path;
        theme.BrightWhite = foreground;

        options.Theme = theme;
        options.MinimumContrastRatio = Math.Max(options.MinimumContrastRatio, 4.5d);
        options.DrawBoldTextInBrightColors = true;

        var selectionBrush = ResolveBrush(SelectionKey);
        if (selectionBrush is not null)
        {
            SelectionBrush = selectionBrush;
        }

        var cursorColor = ResolveSolidColor(CursorKey);
        if (cursorColor.HasValue)
        {
            CursorColor = cursorColor.Value;
        }

        _lastThemeFingerprint = fingerprint;
    }

    private void ApplyFallbackHighlighting(TerminalBuffer? buffer)
    {
        if (Terminal is null || buffer is null || buffer.Length <= 0)
        {
            return;
        }

        var visibleRows = Math.Max(1, Terminal.Rows);
        var viewportY = ResolveViewportY(buffer);
        for (var row = 0; row < visibleRows; row++)
        {
            var absoluteRow = viewportY + row;
            if (absoluteRow < 0 || absoluteRow >= buffer.Length)
            {
                continue;
            }

            var line = buffer.GetLine(absoluteRow);
            if (line is null)
            {
                continue;
            }

            ApplyFallbackHighlightingToLine(line);
        }
    }

    private static void ApplyFallbackHighlightingToLine(BufferLine line)
    {
        var text = line.TranslateToString(trimRight: false, startCol: 0, endCol: line.Length);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var tokenSpans = BuildFallbackTokenSpans(text);
        if (tokenSpans.Count == 0)
        {
            return;
        }

        var columnLimit = Math.Min(line.Length, text.Length);
        for (var col = 0; col < columnLimit; col++)
        {
            var colorIndex = ResolveColorIndexForColumn(tokenSpans, col);
            if (colorIndex < 0)
            {
                continue;
            }

            var cell = line[col];
            if (!ShouldApplyFallback(cell))
            {
                continue;
            }

            var attr = cell.Attributes;
            attr.SetFgColor(colorIndex, PaletteColorMode);
            cell.Attributes = attr;
            line[col] = cell;
        }
    }

    private static bool ShouldApplyFallback(BufferCell cell)
    {
        if (cell.IsEmpty() || string.IsNullOrWhiteSpace(cell.Content))
        {
            return false;
        }

        var attr = cell.Attributes;
        return attr.GetFgColorMode() == PaletteColorMode &&
               attr.GetFgColor() == DefaultFgColor &&
               attr.GetBgColor() == DefaultBgColor &&
               !attr.IsInverse() &&
               !attr.IsInvisible();
    }

    private static List<TokenSpan> BuildFallbackTokenSpans(string text)
    {
        var spans = new List<TokenSpan>();

        // L1: global fallback rules.
        AddRegexMatches(spans, text, ErrorTokenRegex, AnsiBrightRed);
        AddRegexMatches(spans, text, WarnTokenRegex, AnsiBrightYellow);
        AddRegexMatches(spans, text, SuccessTokenRegex, AnsiBrightGreen);
        AddRegexMatches(spans, text, PathTokenRegex, AnsiBrightCyan);
        AddRegexMatches(spans, text, HostTokenRegex, AnsiCyan);
        AddRegexMatches(spans, text, NumberTokenRegex, AnsiBrightYellow);

        // L2: shell-aware enhancement.
        if (ShellPromptRegex.IsMatch(text))
        {
            AddRegexMatches(spans, text, ShellVariableRegex, AnsiBrightMagenta);
            AddRegexMatches(spans, text, ShellOperatorRegex, AnsiBrightBlack);
            AddRegexMatches(spans, text, QuotedSegmentRegex, AnsiBrightGreen);
            AddRegexMatches(spans, text, ShellFlagRegex, AnsiBrightBlue);

            var commandMatch = CommandTokenRegex.Match(text);
            if (commandMatch.Success)
            {
                var group = commandMatch.Groups["cmd"];
                if (group.Success && group.Length > 0)
                {
                    AddSpan(spans, new TokenSpan(group.Index, group.Length, AnsiBrightBlue));
                }
            }
        }

        spans.Sort((a, b) => a.Start.CompareTo(b.Start));
        return spans;
    }

    private static int ResolveColorIndexForColumn(IEnumerable<TokenSpan> spans, int column)
    {
        foreach (var span in spans)
        {
            if (column >= span.Start && column < span.EndExclusive)
            {
                return span.ColorIndex;
            }
        }

        return -1;
    }

    private static void AddRegexMatches(List<TokenSpan> spans, string text, Regex regex, int colorIndex)
    {
        foreach (Match match in regex.Matches(text))
        {
            if (!match.Success || match.Length <= 0)
            {
                continue;
            }

            AddSpan(spans, new TokenSpan(match.Index, match.Length, colorIndex));
        }
    }

    private static void AddSpan(List<TokenSpan> spans, TokenSpan candidate)
    {
        foreach (var existing in spans)
        {
            if (existing.Overlaps(candidate))
            {
                return;
            }
        }

        spans.Add(candidate);
    }

    private void RenderRowStripes(DrawingContext context)
    {
        if (Bounds.Width <= 0d || Bounds.Height <= 0d || Terminal is null)
        {
            return;
        }

        var buffer = Terminal.Buffer;
        if (buffer is null)
        {
            return;
        }

        var evenBrush = ResolveBrush(EvenStripeKey);
        var oddBrush = ResolveBrush(OddStripeKey);
        if (evenBrush is null || oddBrush is null)
        {
            return;
        }

        var visibleRows = Math.Max(1, Terminal.Rows);
        var rowHeight = Bounds.Height / visibleRows;
        var viewportY = ResolveViewportY(buffer);

        for (var row = 0; row < visibleRows; row++)
        {
            var absoluteRow = viewportY + row;
            var brush = absoluteRow % 2 == 0 ? evenBrush : oddBrush;
            var y = row * rowHeight;
            var height = row == visibleRows - 1 ? Bounds.Height - y : rowHeight;
            if (height <= 0d)
            {
                continue;
            }

            context.FillRectangle(brush, new Rect(0d, y, Bounds.Width, height));
        }
    }

    private IBrush? ResolveBrush(string key)
    {
        if (TryGetResource(key, ActualThemeVariant, out var value))
        {
            return value as IBrush;
        }

        if (Application.Current is not null &&
            Application.Current.TryGetResource(key, ActualThemeVariant, out var appValue))
        {
            return appValue as IBrush;
        }

        return null;
    }

    private Color? ResolveSolidColor(string key)
    {
        return ResolveBrush(key) switch
        {
            ISolidColorBrush solid => solid.Color,
            _ => null,
        };
    }

    private string? ResolveHexColor(string key)
    {
        var color = ResolveSolidColor(key);
        return color.HasValue
            ? $"#{color.Value.R:X2}{color.Value.G:X2}{color.Value.B:X2}"
            : null;
    }

    private static int ResolveViewportY(TerminalBuffer buffer)
    {
        return Math.Max(0, buffer.ViewportY);
    }

    private readonly record struct TokenSpan(int Start, int Length, int ColorIndex)
    {
        public int EndExclusive => Start + Length;

        public bool Overlaps(TokenSpan other)
        {
            return Start < other.EndExclusive && other.Start < EndExclusive;
        }
    }
}
