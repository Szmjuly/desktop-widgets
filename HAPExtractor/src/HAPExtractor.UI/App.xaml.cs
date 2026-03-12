using System.Windows;
using System.Windows.Threading;
using HAPExtractor.Core.Services;

namespace HAPExtractor.UI;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Logger.Info("HAP Extractor starting up");
        Logger.Info($"Log file: {Logger.LogFilePath}");

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Fatal("Unhandled UI exception", e.Exception);
        MessageBox.Show(
            $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nDetails logged to:\n{Logger.LogFilePath}",
            "HAP Extractor Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
            Logger.Fatal("Unhandled domain exception", ex);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Logger.Fatal("Unobserved task exception", e.Exception);
        e.SetObserved();
    }
}

