using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SkylarkTerminal.Services;

public static class RuntimeLogger
{
    private static readonly object SyncRoot = new();
    private static readonly string LogFilePath = BuildLogFilePath();

    public static string CurrentLogFilePath => LogFilePath;

    public static string CreateSessionLogFilePath(string sessionId)
    {
        var dateSegment = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var timeSegment = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
        var safeSessionId = string.Concat(
            (sessionId ?? "session")
            .Where(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_'));

        if (string.IsNullOrWhiteSpace(safeSessionId))
        {
            safeSessionId = "session";
        }

        var directory = BuildPrimaryLogDirectory(dateSegment);
        try
        {
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, $"session-{timeSegment}-{safeSessionId}.log");
        }
        catch
        {
            var fallbackDirectory = BuildFallbackLogDirectory(dateSegment);
            Directory.CreateDirectory(fallbackDirectory);
            return Path.Combine(fallbackDirectory, $"session-{timeSegment}-{safeSessionId}.log");
        }
    }

    public static void Info(string category, string message)
    {
        _ = category;
        _ = message;
    }

    public static void Warn(string category, string message)
    {
        _ = category;
        _ = message;
    }

    public static void Error(string category, string message, Exception? exception = null)
    {
        var finalMessage = exception is null
            ? message
            : $"{message} | {exception.GetType().Name}: {exception.Message}";

        Write("ERROR", category, finalMessage);
    }

    private static void Write(string level, string category, string message)
    {
        try
        {
            var line = string.Create(
                CultureInfo.InvariantCulture,
                $"[{DateTimeOffset.Now:O}] [{level}] [{category}] {message}");

            lock (SyncRoot)
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch
        {
        }
    }

    private static string BuildLogFilePath()
    {
        var root = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.GetTempPath();
        }

        var dateSegment = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var timeSegment = DateTime.Now.ToString("HHmmss", CultureInfo.InvariantCulture);
        var primaryDir = BuildPrimaryLogDirectory(dateSegment);

        try
        {
            Directory.CreateDirectory(primaryDir);
            return Path.Combine(primaryDir, $"runtime-{timeSegment}.log");
        }
        catch
        {
            var fallbackDir = BuildFallbackLogDirectory(dateSegment);
            Directory.CreateDirectory(fallbackDir);
            return Path.Combine(fallbackDir, $"runtime-{timeSegment}.log");
        }
    }

    private static string BuildPrimaryLogDirectory(string dateSegment)
    {
        var root = AppContext.BaseDirectory;
        if (string.IsNullOrWhiteSpace(root))
        {
            root = Path.GetTempPath();
        }

        return Path.Combine(root, "logs", dateSegment);
    }

    private static string BuildFallbackLogDirectory(string dateSegment)
    {
        return Path.Combine(Path.GetTempPath(), "SkylarkTerminal", "logs", dateSegment);
    }
}
