using System.Windows;

namespace DesktopHub.UI;

public static class WindowExtensions
{
    public static bool IsClosed(this Window window)
    {
        try
        {
            // A window that was shown and then closed will have IsLoaded=false
            // AND PresentationSource=null. We check both to avoid false positives
            // for windows that simply haven't been shown yet.
            return !window.IsLoaded && System.Windows.PresentationSource.FromVisual(window) == null;
        }
        catch
        {
            return true;
        }
    }
}
