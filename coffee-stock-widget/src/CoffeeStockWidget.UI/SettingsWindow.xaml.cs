using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CoffeeStockWidget.Core.Models;
using CoffeeStockWidget.Infrastructure.Settings;

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

        var settings = new AppSettings { PollIntervalSeconds = seconds };
        await _settingsService.SaveAsync(settings);
        await StartupManager.EnableAsync(RunAtLoginBox.IsChecked == true);
        DialogResult = true;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try { DragMove(); } catch { }
    }
}
