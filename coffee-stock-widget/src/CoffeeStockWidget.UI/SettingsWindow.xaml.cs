using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;
using CoffeeStockWidget.Core.Models;
using CoffeeStockWidget.Infrastructure.Settings;
using CoffeeStockWidget.Infrastructure.Storage;

namespace CoffeeStockWidget.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService = new();
    public List<Source>? Sources { get; set; }
    private ObservableCollection<RoasterRow> _rows = new();
    private enum PendingAction { None, ConfirmClear }
    private PendingAction _pending = PendingAction.None;

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += async (_, __) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var s = await _settingsService.LoadAsync();
        IntervalBox.Text = s.PollIntervalSeconds.ToString();
        RunAtLoginBox.IsChecked = await StartupManager.IsEnabledAsync();
        TransparencySlider.Value = Math.Max(60, Math.Min(100, s.TransparencyPercent));
        BlurToggle.IsChecked = s.BlurEnabled;
        AcrylicToggle.IsChecked = s.AcrylicEnabled;
        AccentTintToggle.IsChecked = s.AccentHoverTintEnabled;
        FetchNotesToggle.IsChecked = s.FetchNotesEnabled;
        MaxNotesFetchBox.Text = s.MaxNotesFetchPerRun.ToString();
        RetentionBox.Text = s.RetentionDays.ToString();
        ItemsCapBox.Text = s.ItemsPerSource.ToString();
        EventsCapBox.Text = s.EventsPerSource.ToString();

        // Build roaster rows
        _rows = new ObservableCollection<RoasterRow>();
        if (Sources != null)
        {
            var enabledSet = s.EnabledParsers != null ? new HashSet<string>(s.EnabledParsers, StringComparer.OrdinalIgnoreCase) : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var colors = s.CustomParserColors != null ? new Dictionary<string, string>(s.CustomParserColors, StringComparer.OrdinalIgnoreCase) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var src in Sources)
            {
                var enabled = enabledSet.Count > 0 ? enabledSet.Contains(src.ParserType) : src.Enabled;
                var colorHex = colors.TryGetValue(src.ParserType, out var hex) ? hex : (src.CustomColorHex ?? src.DefaultColorHex);
                _rows.Add(new RoasterRow
                {
                    Name = src.Name,
                    ParserType = src.ParserType,
                    Enabled = enabled,
                    DefaultColorHex = src.DefaultColorHex,
                    ColorHex = colorHex,
                    ColorBrush = ToBrush(colorHex)
                });
            }
        }
        RoastersPanel.ItemsSource = _rows;
        RoasterColorsPanel.ItemsSource = _rows;
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private async void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(IntervalBox.Text, out var seconds) || seconds < 10)
        {
            System.Windows.MessageBox.Show(this, "Please enter a valid number of seconds (>= 10).", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(RetentionBox.Text, out var days) || days < 1)
        {
            System.Windows.MessageBox.Show(this, "Please enter a valid number of days (>= 1).", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(ItemsCapBox.Text, out var itemsCap) || itemsCap < 0)
        {
            System.Windows.MessageBox.Show(this, "Please enter a valid items cap (>= 0).", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(EventsCapBox.Text, out var eventsCap) || eventsCap < 0)
        {
            System.Windows.MessageBox.Show(this, "Please enter a valid events cap (>= 0).", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!int.TryParse(MaxNotesFetchBox.Text, out var maxNotes) || maxNotes < 0)
        {
            System.Windows.MessageBox.Show(this, "Please enter a valid max notes fetch per run (>= 0).", "Invalid Value", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var existing = await _settingsService.LoadAsync();
        existing.PollIntervalSeconds = seconds;
        var transp = (int)Math.Round(TransparencySlider.Value);
        existing.TransparencyPercent = Math.Max(60, Math.Min(100, transp));
        existing.BlurEnabled = BlurToggle.IsChecked == true;
        existing.AcrylicEnabled = AcrylicToggle.IsChecked == true;
        existing.AccentHoverTintEnabled = AccentTintToggle.IsChecked == true;
        existing.FetchNotesEnabled = FetchNotesToggle.IsChecked == true;
        existing.MaxNotesFetchPerRun = Math.Max(0, Math.Min(50, maxNotes));
        existing.RetentionDays = days;
        existing.ItemsPerSource = itemsCap;
        existing.EventsPerSource = eventsCap;

        // Persist enabled roasters and custom colors
        if (_rows.Count > 0)
        {
            existing.EnabledParsers = _rows.Where(r => r.Enabled).Select(r => r.ParserType).ToList();
            var custom = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in _rows)
            {
                // Store only if different from default; empty values ignored
                if (!string.IsNullOrWhiteSpace(row.ColorHex) && !string.Equals(row.ColorHex, row.DefaultColorHex, StringComparison.OrdinalIgnoreCase))
                {
                    custom[row.ParserType] = row.ColorHex;
                }
            }
            existing.CustomParserColors = custom;
        }

        await _settingsService.SaveAsync(existing);
        await StartupManager.EnableAsync(RunAtLoginBox.IsChecked == true);
        DialogResult = true;
    }

    private void ClearDbBtn_Click(object sender, RoutedEventArgs e)
    {
        ShowDialogOverlay("Confirm Clear", "This will delete all stored coffees and events. Continue?", confirmMode: true);
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try { DragMove(); } catch { }
    }

    private void TransparencySlider_ValueChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        if (Owner is MainWindow mw)
        {
            var transp = (int)Math.Round(TransparencySlider.Value);
            mw.ApplyAppearancePreview(transp, BlurToggle.IsChecked == true, AcrylicToggle.IsChecked == true, AccentTintToggle.IsChecked == true);
        }
    }

    private void AppearanceToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (Owner is MainWindow mw)
        {
            var transp = (int)Math.Round(TransparencySlider.Value);
            mw.ApplyAppearancePreview(transp, BlurToggle.IsChecked == true, AcrylicToggle.IsChecked == true, AccentTintToggle.IsChecked == true);
        }
    }

    private void ShowDialogOverlay(string title, string message, bool confirmMode)
    {
        DialogTitle.Text = title;
        DialogMessage.Text = message;
        DialogCancelBtn.Visibility = confirmMode ? Visibility.Visible : Visibility.Collapsed;
        _pending = confirmMode ? PendingAction.ConfirmClear : PendingAction.None;
        Overlay.Visibility = Visibility.Visible;
    }

    private void DialogCancelBtn_Click(object sender, RoutedEventArgs e)
    {
        _pending = PendingAction.None;
        Overlay.Visibility = Visibility.Collapsed;
    }

    private async void DialogOkBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_pending == PendingAction.ConfirmClear)
        {
            try
            {
                var store = new SqliteDataStore();
                await store.ClearAsync();
                DialogCancelBtn.Visibility = Visibility.Collapsed;
                DialogOkBtn.Content = "Close";
                DialogTitle.Text = "Done";
                DialogMessage.Text = "Database cleared.";
                _pending = PendingAction.None;
                if (Owner is MainWindow mw)
                {
                    mw.OnDatabaseCleared();
                }
            }
            catch (Exception ex)
            {
                DialogCancelBtn.Visibility = Visibility.Collapsed;
                DialogOkBtn.Content = "Close";
                DialogTitle.Text = "Error";
                DialogMessage.Text = "Failed to clear database: " + ex.Message;
                _pending = PendingAction.None;
            }
        }
        else
        {
            Overlay.Visibility = Visibility.Collapsed;
        }
    }

    private void RoasterAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string action && fe.DataContext is RoasterRow row)
        {
            if (string.Equals(action, "apply", StringComparison.OrdinalIgnoreCase))
            {
                row.ColorBrush = ToBrush(row.ColorHex);
            }
            else if (string.Equals(action, "reset", StringComparison.OrdinalIgnoreCase))
            {
                row.ColorHex = row.DefaultColorHex;
                row.ColorBrush = ToBrush(row.ColorHex);
            }
            else if (string.Equals(action, "pick", StringComparison.OrdinalIgnoreCase))
            {
                var dlg = new System.Windows.Forms.ColorDialog
                {
                    AllowFullOpen = true,
                    FullOpen = true
                };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var c = dlg.Color; // System.Drawing.Color
                    var hex = $"#FF{c.R:X2}{c.G:X2}{c.B:X2}";
                    row.ColorHex = hex;
                    row.ColorBrush = ToBrush(hex);
                }
            }
        }
    }

    private static SolidColorBrush ToBrush(string hex)
    {
        try
        {
            var obj = System.Windows.Media.ColorConverter.ConvertFromString(hex);
            if (obj is System.Windows.Media.Color c) return new SolidColorBrush(c);
        }
        catch { }
        return new SolidColorBrush(Colors.DimGray);
    }

    private class RoasterRow : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _parserType = string.Empty;
        private bool _enabled;
        private string _defaultColorHex = "#FF4CAF50";
        private string _colorHex = "#FF4CAF50";
        private SolidColorBrush _colorBrush = new SolidColorBrush(Colors.DimGray);

        public string Name
        {
            get => _name;
            set { if (!string.Equals(_name, value)) { _name = value; OnPropertyChanged(nameof(Name)); } }
        }
        public string ParserType
        {
            get => _parserType;
            set { if (!string.Equals(_parserType, value)) { _parserType = value; OnPropertyChanged(nameof(ParserType)); } }
        }
        public bool Enabled
        {
            get => _enabled;
            set { if (_enabled != value) { _enabled = value; OnPropertyChanged(nameof(Enabled)); } }
        }
        public string DefaultColorHex
        {
            get => _defaultColorHex;
            set { if (!string.Equals(_defaultColorHex, value)) { _defaultColorHex = value; OnPropertyChanged(nameof(DefaultColorHex)); } }
        }
        public string ColorHex
        {
            get => _colorHex;
            set { if (!string.Equals(_colorHex, value)) { _colorHex = value; OnPropertyChanged(nameof(ColorHex)); } }
        }
        public SolidColorBrush ColorBrush
        {
            get => _colorBrush;
            set { if (!Equals(_colorBrush, value)) { _colorBrush = value; OnPropertyChanged(nameof(ColorBrush)); } }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
