using System;
using System.Diagnostics;
using System.Windows.Threading;

namespace DesktopHub.UI.Services;

public enum TimerMode
{
    Stopwatch,
    Timer
}

public class TimerService
{
    private readonly Stopwatch _stopwatch;
    private readonly DispatcherTimer _uiUpdateTimer;
    private TimeSpan _timerDuration;
    private DateTime _timerStartTime;
    private bool _isRunning;
    private TimerMode _mode;
    
    public event EventHandler<TimeSpan>? TimeUpdated;
    public event EventHandler? TimerCompleted;
    
    public bool IsRunning => _isRunning;
    public TimerMode Mode => _mode;
    
    public TimerService()
    {
        _stopwatch = new Stopwatch();
        _uiUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _uiUpdateTimer.Tick += OnTimerTick;
        _mode = TimerMode.Stopwatch;
    }
    
    public void Start()
    {
        if (_isRunning) return;
        
        _isRunning = true;
        
        if (_mode == TimerMode.Stopwatch)
        {
            _stopwatch.Start();
        }
        else
        {
            _timerStartTime = DateTime.Now;
        }
        
        _uiUpdateTimer.Start();
    }
    
    public void Pause()
    {
        if (!_isRunning) return;
        
        _isRunning = false;
        
        if (_mode == TimerMode.Stopwatch)
        {
            _stopwatch.Stop();
        }
        
        _uiUpdateTimer.Stop();
    }
    
    public void Reset()
    {
        _isRunning = false;
        _stopwatch.Reset();
        _uiUpdateTimer.Stop();
        TimeUpdated?.Invoke(this, TimeSpan.Zero);
    }
    
    public void SetMode(TimerMode mode)
    {
        if (_mode == mode) return;
        
        Reset();
        _mode = mode;
    }
    
    public void SetTimerDuration(TimeSpan duration)
    {
        _timerDuration = duration;
        if (!_isRunning)
        {
            TimeUpdated?.Invoke(this, duration);
        }
    }
    
    public TimeSpan GetCurrentTime()
    {
        if (_mode == TimerMode.Stopwatch)
        {
            return _stopwatch.Elapsed;
        }
        else
        {
            if (!_isRunning)
                return _timerDuration;
            
            var elapsed = DateTime.Now - _timerStartTime;
            var remaining = _timerDuration - elapsed;
            
            if (remaining <= TimeSpan.Zero)
            {
                remaining = TimeSpan.Zero;
                if (_isRunning)
                {
                    Pause();
                    TimerCompleted?.Invoke(this, EventArgs.Empty);
                }
            }
            
            return remaining;
        }
    }
    
    private void OnTimerTick(object? sender, EventArgs e)
    {
        var currentTime = GetCurrentTime();
        TimeUpdated?.Invoke(this, currentTime);
    }
}
