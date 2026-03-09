using System;
using System.Collections.Generic;

namespace SkylarkTerminal.Services;

public sealed class SftpNavigationService : ISftpNavigationService
{
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();
    private readonly List<string> _recentPaths = [];

    public SftpNavigationService(string initialPath)
    {
        CurrentPath = NormalizePath(initialPath);
        RememberPath(CurrentPath);
    }

    public string CurrentPath { get; private set; }

    public bool CanGoBack => _backStack.Count > 0;

    public bool CanGoForward => _forwardStack.Count > 0;

    public IReadOnlyList<string> RecentPaths => _recentPaths;

    public string NavigateTo(string path)
    {
        var targetPath = NormalizePath(path);
        if (string.Equals(targetPath, CurrentPath, StringComparison.Ordinal))
        {
            return CurrentPath;
        }

        _backStack.Push(CurrentPath);
        _forwardStack.Clear();
        CurrentPath = targetPath;
        RememberPath(CurrentPath);
        return CurrentPath;
    }

    public string GoBack()
    {
        if (!CanGoBack)
        {
            return CurrentPath;
        }

        _forwardStack.Push(CurrentPath);
        CurrentPath = _backStack.Pop();
        RememberPath(CurrentPath);
        return CurrentPath;
    }

    public string GoForward()
    {
        if (!CanGoForward)
        {
            return CurrentPath;
        }

        _backStack.Push(CurrentPath);
        CurrentPath = _forwardStack.Pop();
        RememberPath(CurrentPath);
        return CurrentPath;
    }

    public string GoUp()
    {
        if (string.Equals(CurrentPath, "/", StringComparison.Ordinal))
        {
            return CurrentPath;
        }

        var lastSlash = CurrentPath.LastIndexOf('/');
        var parentPath = lastSlash <= 0 ? "/" : CurrentPath[..lastSlash];
        return NavigateTo(parentPath);
    }

    public string Refresh()
    {
        return CurrentPath;
    }

    public string TryResolveAddressInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return CurrentPath;
        }

        var normalized = NormalizePath(input);
        return NavigateTo(normalized);
    }

    private void RememberPath(string path)
    {
        _recentPaths.RemoveAll(existing => string.Equals(existing, path, StringComparison.Ordinal));
        _recentPaths.Insert(0, path);
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var value = path.Trim().Replace('\\', '/');
        if (!value.StartsWith("/", StringComparison.Ordinal))
        {
            value = "/" + value;
        }

        while (value.Length > 1 && value.EndsWith("/", StringComparison.Ordinal))
        {
            value = value[..^1];
        }

        return value;
    }
}
