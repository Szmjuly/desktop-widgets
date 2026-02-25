using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopHub.Core.Models;
using DesktopHub.UI.Services;

namespace DesktopHub.UI.Widgets;

public partial class TimerWidget : System.Windows.Controls.UserControl
{
    private readonly TimerService _timerService;
    private bool _isInitialized = false;
    
    public TimerWidget(TimerService timerService)
    {
        try
        {
            if (timerService == null)
                throw new ArgumentNullException(nameof(timerService));
                
            InitializeComponent();
            _timerService = timerService;
            
            _timerService.TimeUpdated += OnTimeUpdated;
            _timerService.TimerCompleted += OnTimerCompleted;
            
            Loaded += (s, e) =>
            {
                try
                {
                    _isInitialized = true;
                    UpdateModeUI();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error in UpdateModeUI: {ex.Message}\n\nStack: {ex.StackTrace}", "Timer Widget Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error initializing TimerWidget: {ex.Message}\n\nStack: {ex.StackTrace}", "Timer Widget Error", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
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

            // Track timer stop
            TelemetryAccessor.TrackTimer(TelemetryEventType.TimerStopped);
        }
        else
        {
            _timerService.Start();
            StartPauseText.Text = "Pause";

            // Track timer start
            TelemetryAccessor.TrackTimer(TelemetryEventType.TimerStarted);
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
}
