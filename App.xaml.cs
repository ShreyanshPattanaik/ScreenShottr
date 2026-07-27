using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;

namespace ShottrClone;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private const string InstanceMutexName = @"Local\ScreenShottr.SingleInstance";
    private const string ActivationEventName = @"Local\ScreenShottr.Activate";
    private Mutex? _instanceMutex;
    private bool _ownsInstanceMutex;
    private EventWaitHandle? _activationEvent;
    private CancellationTokenSource? _activationCancellation;

    protected override void OnStartup(StartupEventArgs e)
    {
        _instanceMutex = new Mutex(true, InstanceMutexName, out var isFirstInstance);
        _ownsInstanceMutex = isFirstInstance;
        if (!isFirstInstance)
        {
            try
            {
                using var activation = EventWaitHandle.OpenExisting(ActivationEventName);
                activation.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                // The first instance is still starting; exiting remains the safe behavior.
            }
            Shutdown();
            return;
        }

        _activationEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            ActivationEventName);
        _activationCancellation = new CancellationTokenSource();
        StartActivationListener(_activationCancellation.Token);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);

        var window = new ShottrClone.MainWindow();
        MainWindow = window;
        window.Show();
    }

    private void StartActivationListener(CancellationToken cancellationToken)
    {
        _ = Task.Run(() =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (_activationEvent?.WaitOne(500) != true)
                    continue;

                Dispatcher.BeginInvoke(() =>
                {
                    if (MainWindow is not { } window)
                        return;
                    window.Show();
                    window.WindowState = WindowState.Normal;
                    window.Activate();
                });
            }
        }, cancellationToken);
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"ScreenShottr could not complete that action.\n\n{e.Exception.Message}",
            "ScreenShottr",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _activationCancellation?.Cancel();
        _activationEvent?.Set();
        _activationEvent?.Dispose();
        _activationCancellation?.Dispose();
        if (_ownsInstanceMutex)
            _instanceMutex?.ReleaseMutex();
        _instanceMutex?.Dispose();
        base.OnExit(e);
    }
}

