using System;
using System.Configuration;
using System.Data;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace CoffeeStockWidget.UI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static bool _shownDispatcherCrash;
    public App()
    {
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private static void WriteCrash(string title, Exception ex)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine(title);
            sb.AppendLine(DateTime.Now.ToString("u"));
            sb.AppendLine(ex.ToString());
            if (ex.InnerException != null)
            {
                sb.AppendLine("Inner:");
                sb.AppendLine(ex.InnerException.ToString());
            }
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Crash.log");
            File.WriteAllText(path, sb.ToString());
        }
        catch { }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        WriteCrash("DispatcherUnhandledException", e.Exception);
        if (!_shownDispatcherCrash)
        {
            _shownDispatcherCrash = true;
            System.Windows.MessageBox.Show("A fatal error occurred. Details were written to Crash.log in the app folder.\n" + e.Exception.Message, "Coffee Stock Widget", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        e.Handled = true;
        // Shutdown to avoid message storm
        try { Current.Shutdown(); } catch { }
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            WriteCrash("UnhandledException", ex);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        WriteCrash("UnobservedTaskException", e.Exception);
    }
}

