using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CoffeeStockWidget.Core.Models;
using CoffeeStockWidget.Infrastructure.Settings;
using CoffeeStockWidget.Infrastructure.Storage;

namespace CoffeeStockWidget.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService = new();

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
        TransparencySlider.Value = s.TransparencyPercent;
        BlurToggle.IsChecked = s.BlurEnabled;
        RetentionBox.Text = s.RetentionDays.ToString();
        ItemsCapBox.Text = s.ItemsPerSource.ToString();
        EventsCapBox.Text = s.EventsPerSource.ToString();
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

        var existing = await _settingsService.LoadAsync();
        existing.PollIntervalSeconds = seconds;
        existing.TransparencyPercent = (int)Math.Round(TransparencySlider.Value);
        existing.BlurEnabled = BlurToggle.IsChecked == true;
        existing.RetentionDays = days;
        existing.ItemsPerSource = itemsCap;
        existing.EventsPerSource = eventsCap;

        await _settingsService.SaveAsync(existing);
        await StartupManager.EnableAsync(RunAtLoginBox.IsChecked == true);
        DialogResult = true;
    }

    private async void ClearDbBtn_Click(object sender, RoutedEventArgs e)
    {
        var confirm = System.Windows.MessageBox.Show(this, "This will delete all stored coffees and events. Continue?", "Confirm Clear", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;
        try
        {
            var store = new SqliteDataStore();
            await store.ClearAsync();
            System.Windows.MessageBox.Show(this, "Database cleared.", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            if (Owner is MainWindow mw)
            {
                mw.OnDatabaseCleared();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, "Failed to clear database: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try { DragMove(); } catch { }
    }
}
