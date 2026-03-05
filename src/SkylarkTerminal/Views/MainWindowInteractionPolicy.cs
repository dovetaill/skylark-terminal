namespace SkylarkTerminal.Views;

public static class MainWindowInteractionPolicy
{
    public static bool ShouldCloseAssetsSearchOnPointerPressed(
        bool isAssetsSearchOpen,
        string? assetsSearchText,
        bool isLeftButtonPressed,
        bool isPointerInsideSearchBox,
        bool isPointerInsideSearchToggleButton)
    {
        if (!isAssetsSearchOpen || !string.IsNullOrWhiteSpace(assetsSearchText))
        {
            return false;
        }

        if (!isLeftButtonPressed)
        {
            return false;
        }

        return !isPointerInsideSearchBox && !isPointerInsideSearchToggleButton;
    }
}
