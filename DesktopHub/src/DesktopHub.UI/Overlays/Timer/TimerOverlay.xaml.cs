using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopHub.UI.Services;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Helpers;

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
            };
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error initializing TimerOverlay: {ex.Message}", "Timer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }
    
    public void EnableDragging() => OverlayDragHelper.EnableDragging(this);
    public void DisableDragging() => OverlayDragHelper.DisableDragging(this);
    
    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Hide();
    }
    
    private void OnTimeUpdated(object? sender, TimeSpan time)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (TimeDisplay != null)
            {
                TimeDisplay.Text = FormatTime(time);
            }
        });
    }
    
    private void OnTimerCompleted(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
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
            StopwatchButton.Background = Helpers.ThemeHelper.HoverMedium;
            TimerButton.Background = System.Windows.Media.Brushes.Transparent;
            TimerInputPanel.Visibility = Visibility.Collapsed;
        }
        else
        {
            StopwatchButton.Background = System.Windows.Media.Brushes.Transparent;
            TimerButton.Background = Helpers.ThemeHelper.HoverMedium;
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
    
    public void SetUpdateIndicatorVisible(bool visible) =>
        OverlayHelper.SetUpdateIndicatorVisible(UpdateIndicator, visible);

    public void UpdateTransparency() =>
        OverlayHelper.ApplyTransparency(RootBorder, _settings.GetTimerWidgetTransparency(), "TimerOverlay");
    
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (OverlayHelper.IsCloseShortcutPressed(e, _settings))
        {
            DebugLogger.Log("TimerOverlay: Close shortcut pressed -> Hiding");
            Visibility = Visibility.Hidden;
            e.Handled = true;
        }
    }
    
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!OverlayHelper.HandleOnClosingHide(e, this))
            base.OnClosing(e);
    }
}
