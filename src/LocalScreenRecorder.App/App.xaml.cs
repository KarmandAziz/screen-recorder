using System.Windows;
using System.Windows.Threading;
using LocalScreenRecorder.App.Services;
using LocalScreenRecorder.App.ViewModels;
using LocalScreenRecorder.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LocalScreenRecorder.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        _services = ConfigureServices();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            var viewModel = _services.GetRequiredService<MainViewModel>();
            await viewModel.InitializeAsync();
            var window = _services.GetRequiredService<MainWindow>();
            MainWindow = window;
            _services.GetRequiredService<ILoggingService>().Info("Application initialized successfully.");
            window.Show();
        }
        catch (Exception exception)
        {
            _services.GetRequiredService<ILoggingService>().Error("Application startup failed.", exception);
            MessageBox.Show(
                "The recorder could not start. Ensure the Visual C++ 2015–2022 x64 runtime and Windows Media Foundation are installed.",
                "Startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
        _services?.Dispose();
        base.OnExit(e);
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<SettingsSerializer>();
        services.AddSingleton<QualityPresetService>();
        services.AddSingleton<FilenameService>();
        services.AddSingleton<RegionCoordinateConverter>();
        services.AddSingleton<HotkeyValidator>();
        services.AddSingleton<ILoggingService, LoggingService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IDisplayService, DisplayService>();
        services.AddSingleton<IAudioMixerService, AudioMixerService>();
        services.AddSingleton<IAudioCaptureService, AudioCaptureService>();
        services.AddSingleton<IScreenCaptureService, ScreenCaptureService>();
        services.AddSingleton<IEncoderService, EncoderService>();
        services.AddSingleton<IRecordingService, RecordingService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<IRegionSelectionService, RegionSelectionService>();
        services.AddSingleton<IRecordingIndicatorService, RecordingIndicatorService>();
        services.AddSingleton<IFolderPickerService, FolderPickerService>();
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
        return services.BuildServiceProvider();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _services?.GetService<ILoggingService>()?.Error("Unhandled UI exception.", e.Exception);
        MessageBox.Show("An unexpected error occurred. Technical details were written to the local log.",
            "Screen Recorder", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        _services?.GetService<ILoggingService>()?.Error("Unhandled application exception.", e.ExceptionObject as Exception);
    }
}
