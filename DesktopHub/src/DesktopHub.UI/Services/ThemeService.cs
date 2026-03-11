using Microsoft.Win32;
using DesktopHub.Core.Abstractions;

namespace DesktopHub.UI.Services;

/// <summary>
/// Manages runtime theme switching (Dark / Light / System).
/// Swaps the Color resource dictionary in Application.Resources.MergedDictionaries[0].
/// Brushes in ThemeBrushes.xaml auto-update because they bind Color via DynamicResource.
/// </summary>
public sealed class ThemeService : IDisposable
{
    public const string ThemeDark = "Dark";
    public const string ThemeLight = "Light";
    public const string ThemeCoffee = "Coffee";
    public const string ThemeSystem = "System";

    private const string DarkColorsUri = "Styles/Themes/DarkColors.xaml";
    private const string LightColorsUri = "Styles/Themes/LightColors.xaml";
    private const string CoffeeColorsUri = "Styles/Themes/CoffeeColors.xaml";

    private readonly ISettingsService _settings;
    private string _currentResolvedTheme = ThemeDark;

    /// <summary>Raised after the theme has been applied.</summary>
    public event Action<string>? ThemeChanged;

    public ThemeService(ISettingsService settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// The currently active resolved theme ("Dark" or "Light").
    /// When the setting is "System", this reflects the OS preference.
    /// </summary>
    public string CurrentTheme => _currentResolvedTheme;

    /// <summary>
    /// The raw setting value ("Dark", "Light", or "System").
    /// </summary>
    public string ThemeSetting => _settings.GetTheme();

    /// <summary>
    /// Initialize theme on startup. Call once from App or main window.
    /// </summary>
    public void Initialize()
    {
        ApplyTheme(_settings.GetTheme());
        StartSystemThemeWatcher();
    }

    /// <summary>
    /// Set and apply a new theme. Persists to settings.
    /// </summary>
    public void SetTheme(string theme)
    {
        _settings.SetTheme(theme);
        _settings.SaveAsync().ConfigureAwait(false);
        ApplyTheme(theme);
    }

    /// <summary>
    /// Apply the theme without persisting (used internally and on startup).
    /// </summary>
    private void ApplyTheme(string themeSetting)
    {
        var resolved = ResolveTheme(themeSetting);
        if (resolved == _currentResolvedTheme && System.Windows.Application.Current?.Resources.MergedDictionaries.Count > 0)
            return;

        _currentResolvedTheme = resolved;

        var uri = resolved switch
        {
            ThemeLight => LightColorsUri,
            ThemeCoffee => CoffeeColorsUri,
            _ => DarkColorsUri
        };
        var newDict = new System.Windows.ResourceDictionary
        {
            Source = new Uri(uri, UriKind.Relative)
        };

        var mergedDicts = System.Windows.Application.Current?.Resources.MergedDictionaries;
        if (mergedDicts == null || mergedDicts.Count == 0) return;

        // Clear and rebuild the entire chain so all DynamicResource references re-resolve
        mergedDicts.Clear();
        mergedDicts.Add(newDict);
        mergedDicts.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri("Styles/ThemeBrushes.xaml", UriKind.Relative)
        });
        mergedDicts.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri("Styles/SharedStyles.xaml", UriKind.Relative)
        });

        ThemeChanged?.Invoke(resolved);
    }

    /// <summary>
    /// Resolve "System" to the actual OS theme, or pass through "Dark"/"Light".
    /// </summary>
    private static string ResolveTheme(string themeSetting)
    {
        if (string.Equals(themeSetting, ThemeSystem, StringComparison.OrdinalIgnoreCase))
            return GetSystemTheme();

        if (string.Equals(themeSetting, ThemeLight, StringComparison.OrdinalIgnoreCase))
            return ThemeLight;

        if (string.Equals(themeSetting, ThemeCoffee, StringComparison.OrdinalIgnoreCase))
            return ThemeCoffee;

        return ThemeDark;
    }

    /// <summary>
    /// Detect Windows dark/light mode from registry.
    /// </summary>
    private static string GetSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i)
                return i == 1 ? ThemeLight : ThemeDark;
        }
        catch
        {
            // Fall back to dark if registry read fails
        }
        return ThemeDark;
    }

    #region System Theme Watcher

    private bool _watcherStarted;

    private void StartSystemThemeWatcher()
    {
        if (_watcherStarted) return;
        _watcherStarted = true;

        // Listen for Windows theme changes via SystemEvents
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category != UserPreferenceCategory.General) return;

        // Only react if current setting is "System"
        if (!string.Equals(_settings.GetTheme(), ThemeSystem, StringComparison.OrdinalIgnoreCase))
            return;

        var newResolved = GetSystemTheme();
        if (newResolved == _currentResolvedTheme) return;

        // Must dispatch to UI thread
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            ApplyTheme(ThemeSystem);
        });
    }

    #endregion

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
    }
}
