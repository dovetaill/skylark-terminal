using Avalonia;
using SkylarkTerminal.Models;
using System;

namespace SkylarkTerminal.Views;

public static class WorkspacePaneDropPolicy
{
    public static WorkspaceDropDirection? ResolveDropDirection(
        Point point,
        double width,
        double height,
        double minPaneSize)
    {
        const double minEdge = 40d;
        const double edgeRatio = 0.22d;

        if (width <= 0d || height <= 0d)
        {
            return null;
        }

        var leftEdge = Math.Max(minEdge, width * edgeRatio);
        var rightEdge = width - leftEdge;
        var topEdge = Math.Max(minEdge, height * edgeRatio);
        var bottomEdge = height - topEdge;

        var leftDistance = point.X;
        var rightDistance = width - point.X;
        var topDistance = point.Y;
        var bottomDistance = height - point.Y;

        var canSplitHorizontally = width >= minPaneSize * 2d;
        var canSplitVertically = height >= minPaneSize * 2d;

        var canDropLeft = canSplitHorizontally && point.X <= leftEdge;
        var canDropRight = canSplitHorizontally && point.X >= rightEdge;
        var canDropTop = canSplitVertically && point.Y <= topEdge;
        var canDropBottom = canSplitVertically && point.Y >= bottomEdge;

        if (!canDropLeft && !canDropRight && !canDropTop && !canDropBottom)
        {
            return null;
        }

        var minDistance = double.MaxValue;
        WorkspaceDropDirection? chosen = null;

        if (canDropLeft && leftDistance < minDistance)
        {
            minDistance = leftDistance;
            chosen = WorkspaceDropDirection.Left;
        }

        if (canDropRight && rightDistance < minDistance)
        {
            minDistance = rightDistance;
            chosen = WorkspaceDropDirection.Right;
        }

        if (canDropTop && topDistance < minDistance)
        {
            minDistance = topDistance;
            chosen = WorkspaceDropDirection.Top;
        }

        if (canDropBottom && bottomDistance < minDistance)
        {
            chosen = WorkspaceDropDirection.Bottom;
        }

        return chosen;
    }
}
