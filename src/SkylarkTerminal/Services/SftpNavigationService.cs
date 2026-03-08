using System;
using System.Collections.Generic;

namespace SkylarkTerminal.Services;

public sealed class SftpNavigationService : ISftpNavigationService
{
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();

    public SftpNavigationService(string initialPath)
    {
        CurrentPath = NormalizePath(initialPath);
    }

    public string CurrentPath { get; private set; }

    public bool CanGoBack => _backStack.Count > 0;

    public bool CanGoForward => _forwardStack.Count > 0;

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
