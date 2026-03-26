using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using TeamsNowPlaying.Services;
using TeamsNowPlaying.ViewModels;
using TeamsNowPlaying.Views;

namespace TeamsNowPlaying;

[System.Diagnostics.CodeAnalysis.SuppressMessage("IDisposable", "CA1001:Types that own disposable fields should be disposable",
    Justification = "Avalonia Application base class doesn't support IDisposable; _trayIcon is disposed on exit")]
public partial class App : Application
{
    private SettingsService _settings = null!;
    private SpotifyService _spotify = null!;
    private TeamsService _teams = null!;
    private NowPlayingService _nowPlaying = null!;
    private MainWindowViewModel _viewModel = null!;
    private MainWindow? _mainWindow;
    private TrayIcon? _trayIcon;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            // Wire up services
            _settings = new SettingsService();
            _settings.Load();

            _spotify = new SpotifyService(_settings);
            _teams = new TeamsService(_settings);
            _nowPlaying = new NowPlayingService(_spotify, _teams, _settings);
            _viewModel = new MainWindowViewModel(_settings, _spotify, _teams, _nowPlaying);

            // Set up tray icon in code
            SetupTrayIcon(desktop);

            // Update tray tooltip when track changes
            _nowPlaying.TrackChanged += track =>
            {
                if (_trayIcon is not null)
                {
                    _trayIcon.ToolTipText = track is not null
                        ? track.Format("{artist} - {title}")
                        : "Teams Now Playing — Idle";
                }
            };

            // Don't show main window on startup; hide to tray
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Restore connections at startup
            _ = _viewModel.TryRestoreConnectionsAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var openSettingsItem = new NativeMenuItem("Open Settings");
        openSettingsItem.Click += (_, _) => ShowMainWindow(desktop);

        var exitItem = new NativeMenuItem("Exit");
        exitItem.Click += (_, _) =>
        {
            _nowPlaying.Stop();
            _trayIcon?.Dispose();
            desktop.TryShutdown();
        };

        var menu = new NativeMenu();
        menu.Items.Add(openSettingsItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exitItem);

        _trayIcon = new TrayIcon
        {
            ToolTipText = "Teams Now Playing",
            Menu = menu,
            Icon = new WindowIcon(
                AssetLoader.Open(new Uri("avares://TeamsNowPlaying/Assets/app-icon.ico")))
        };
        _trayIcon.Clicked += (_, _) => ShowMainWindow(desktop);
        _trayIcon.IsVisible = true;
    }

    private void ShowMainWindow(IClassicDesktopStyleApplicationLifetime desktop)
    {
        if (_mainWindow is null || !_mainWindow.IsVisible)
        {
            _mainWindow = new MainWindow { DataContext = _viewModel };
            _mainWindow.Closing += (_, e) =>
            {
                e.Cancel = true;
                _mainWindow.Hide();
            };
            _mainWindow.Show();
        }
        else
        {
            _mainWindow.Activate();
        }
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "DataValidators is safe to access; we only remove the plugin at startup.")]
    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in dataValidationPluginsToRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
