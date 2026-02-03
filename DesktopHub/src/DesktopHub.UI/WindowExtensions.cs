using System.Windows;

namespace DesktopHub.UI;

public static class WindowExtensions
{
    public static bool IsClosed(this Window window)
    {
        try
        {
            return !window.IsLoaded;
        }
        catch
        {
            return true;
        }
    }
}
