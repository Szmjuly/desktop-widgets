using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopHub.UI.Services;

namespace DesktopHub.UI.Widgets;

public partial class TimerWidget : UserControl
{
    private readonly TimerService _timerService;
    
    public TimerWidget(TimerService timerService)
    {
        InitializeComponent();
        _timerService = timerService;
        
        _timerService.TimeUpdated += OnTimeUpdated;
        _timerService.TimerCompleted += OnTimerCompleted;
        
        UpdateModeUI();
    }
    
    private void OnTimeUpdated(object? sender, TimeSpan time)
    {
        Dispatcher.Invoke(() =>
        {
            TimeDisplay.Text = FormatTime(time);
        });
    }
    
    private void OnTimerCompleted(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            MessageBox.Show("Timer completed!", "Timer", MessageBoxButton.OK, MessageBoxImage.Information);
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
        if (_timerService.Mode == TimerMode.Stopwatch)
        {
            StopwatchButton.Background = System.Windows.Media.Brushes.Transparent;
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
        UpdateTimerDuration();
    }
    
    private void UpdateTimerDuration()
    {
        if (_timerService.Mode == TimerMode.Timer)
        {
            if (int.TryParse(HoursInput.Text, out int hours) &&
                int.TryParse(MinutesInput.Text, out int minutes) &&
                int.TryParse(SecondsInput.Text, out int seconds))
            {
                var duration = new TimeSpan(hours, minutes, seconds);
                _timerService.SetTimerDuration(duration);
            }
        }
    }
}
