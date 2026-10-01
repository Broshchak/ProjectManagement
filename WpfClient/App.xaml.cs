using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WpfClient;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        ShowAndLogException(e.Exception);
        e.Handled = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            ShowAndLogException(exception);
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        ShowAndLogException(e.Exception);
        e.SetObserved();
    }

    private static void ShowAndLogException(Exception exception)
    {
        string message = exception.ToString();
        File.AppendAllText("wpf-client-errors.log", $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\r\n{message}\r\n\r\n");
        MessageBox.Show(
            exception.Message,
            "Помилка програми",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
