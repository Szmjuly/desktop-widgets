using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopHub.UI.Services;
using DesktopHub.Core.Abstractions;

namespace DesktopHub.UI;

public partial class TimerOverlay : Window
{
    private readonly TimerService _timerService;
    private readonly ISettingsService _settings;
    private bool _isInitialized = false;
    
    public TimerOverlay(TimerService timerService, ISettingsService settings)
    {
        try
        {
            if (timerService == null)
                throw new ArgumentNullException(nameof(timerService));
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
                
            InitializeComponent();
            _timerService = timerService;
            _settings = settings;
            
            _timerService.TimeUpdated += OnTimeUpdated;
            _timerService.TimerCompleted += OnTimerCompleted;
            
            Loaded += (s, e) =>
            {
                _isInitialized = true;
                UpdateModeUI();
                PositionWindow();
            };
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error initializing TimerOverlay: {ex.Message}", "Timer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }
    
    private void PositionWindow()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - 20;
        Top = workArea.Bottom - Height - 20;
    }
    
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }
    
    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Hide();
    }
    
    private void OnTimeUpdated(object? sender, TimeSpan time)
    {
        Dispatcher.Invoke(() =>
        {
            if (TimeDisplay != null)
            {
                TimeDisplay.Text = FormatTime(time);
            }
        });
    }
    
    private void OnTimerCompleted(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            System.Windows.MessageBox.Show("Timer completed!", "Timer", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }
    
    private string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";
    }
    
    private void StopwatchButton_Click(object sender, MouseButtonEventArgs e)
    {
        _timerService.SetMode(TimerMode.Stopwatch);
        UpdateModeUI();
    }
    
    private void TimerButton_Click(object sender, MouseButtonEventArgs e)
    {
        _timerService.SetMode(TimerMode.Timer);
        UpdateModeUI();
    }
    
    private void UpdateModeUI()
    {
        if (StopwatchButton == null || TimerButton == null || TimerInputPanel == null)
            return;
            
        if (_timerService.Mode == TimerMode.Stopwatch)
        {
            StopwatchButton.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#20F5F7FA"));
            TimerButton.Background = System.Windows.Media.Brushes.Transparent;
            TimerInputPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            StopwatchButton.Background = System.Windows.Media.Brushes.Transparent;
            TimerButton.Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#20F5F7FA"));
            TimerInputPanel.Visibility = Visibility.Visible;
        }
        
        UpdateTimerDuration();
    }
    
    private void StartPauseButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (_timerService.IsRunning)
        {
            _timerService.Pause();
            StartPauseText.Text = "Start";
        }
        else
        {
            _timerService.Start();
            StartPauseText.Text = "Pause";
        }
    }
    
    private void ResetButton_Click(object sender, MouseButtonEventArgs e)
    {
        _timerService.Reset();
        StartPauseText.Text = "Start";
        
        if (_timerService.Mode == TimerMode.Timer)
        {
            HoursInput.Text = "0";
            MinutesInput.Text = "0";
            SecondsInput.Text = "0";
        }
    }
    
    private void TimerInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isInitialized)
            return;
            
        UpdateTimerDuration();
    }
    
    private void UpdateTimerDuration()
    {
        if (_timerService.Mode == TimerMode.Timer)
        {
            if (HoursInput != null && MinutesInput != null && SecondsInput != null &&
                int.TryParse(HoursInput.Text, out int hours) &&
                int.TryParse(MinutesInput.Text, out int minutes) &&
                int.TryParse(SecondsInput.Text, out int seconds))
            {
                var duration = new TimeSpan(hours, minutes, seconds);
                _timerService.SetTimerDuration(duration);
            }
        }
    }
    
    public void UpdateTransparency()
    {
        try
        {
            var transparency = _settings.GetTimerWidgetTransparency();
            var alpha = (byte)(transparency * 255);
            
            if (RootBorder != null)
            {
                RootBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x18, 0x18, 0x18));
            }
            
            DebugLogger.Log($"TimerOverlay: Transparency updated to {transparency:F2}");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"TimerOverlay: UpdateTransparency error: {ex.Message}");
        }
    }
    
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }
}
