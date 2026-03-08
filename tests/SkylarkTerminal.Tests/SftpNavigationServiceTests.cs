using SkylarkTerminal.Services;

namespace SkylarkTerminal.Tests;

public class SftpNavigationServiceTests
{
    [Fact]
    public void Navigate_Back_Forward_ShouldMaintainStacks()
    {
        var nav = new SftpNavigationService("/");
        nav.NavigateTo("/var");
        nav.NavigateTo("/var/log");

        Assert.True(nav.CanGoBack);
        Assert.Equal("/var", nav.GoBack());
        Assert.True(nav.CanGoForward);
        Assert.Equal("/var/log", nav.GoForward());
    }

    [Fact]
    public void GoUp_ShouldResolveParentPath()
    {
        var nav = new SftpNavigationService("/var/log");
        Assert.Equal("/var", nav.GoUp());
    }
}
