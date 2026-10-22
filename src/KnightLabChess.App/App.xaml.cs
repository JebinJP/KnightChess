using System.Diagnostics;
using System.IO;
using System.Windows;
using KnightLabChess.App.ViewModels;
using KnightLabChess.Core.Audio;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Coach;
using KnightLabChess.Core.Engine;
using KnightLabChess.Core.Lessons;
using KnightLabChess.Core.Persistence;
using KnightLabChess.Core.Progress;
using KnightLabChess.Core.Puzzles;
using KnightLabChess.Infrastructure.Audio;
using KnightLabChess.Infrastructure.Coach;
using KnightLabChess.Infrastructure.Content;
using KnightLabChess.Infrastructure.Engine;
using KnightLabChess.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace KnightLabChess.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private readonly Stopwatch _startupStopwatch = Stopwatch.StartNew();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Global exception handlers to prevent crashes
        DispatcherUnhandledException += (s, args) =>
        {
            Debug.WriteLine($"[KnightLab] Unhandled UI exception: {args.Exception}");
            args.Handled = true; // Prevent crash
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Debug.WriteLine($"[KnightLab] Unhandled domain exception: {ex}");
            }
        };

        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            Debug.WriteLine($"[KnightLab] Unobserved task exception: {args.Exception}");
            args.SetObserved(); // Prevent crash
        };

        ShutdownMode = ShutdownMode.OnMainWindowClose;

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Initialize SQLite database synchronously
        try
        {
            var db = _serviceProvider.GetRequiredService<DatabaseManager>();
            db.InitializeAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to initialize application database: {ex.Message}",
                "KnightLab Chess Database Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }

        // Show main window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.ContentRendered += (s, args) =>
        {
            _startupStopwatch.Stop();
            Debug.WriteLine($"[KnightLab Benchmark] Cold startup time to ContentRendered: {_startupStopwatch.ElapsedMilliseconds} ms");
        };

        mainWindow.Show();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddDebug();
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Debug);
#else
            builder.SetMinimumLevel(LogLevel.Information);
#endif
        });

        // Core domain & Engine
        services.AddTransient<IChessGame, GeraChessGameAdapter>();
        services.AddSingleton<IEngineService, UciStockfishEngine>();

        // Audio Service
        services.AddSingleton<IAudioService>(sp =>
        {
            var contentDir = AppDomain.CurrentDomain.BaseDirectory;
            return new ChessAudioService(contentDir, sp.GetRequiredService<ILogger<ChessAudioService>>());
        });

        // AI Chess Coach
        services.AddSingleton<IAiCoachService, GeminiCoachService>();

        // Infrastructure Persistence
        services.AddSingleton<DatabaseManager>(sp =>
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbFolder = Path.Combine(appData, "KnightLabChess");
            Directory.CreateDirectory(dbFolder);
            var dbPath = Path.Combine(dbFolder, "knightlab.db");
            return new DatabaseManager(dbPath, sp.GetRequiredService<ILogger<DatabaseManager>>());
        });
        services.AddSingleton<IGameRepository, SqliteGameRepository>();
        services.AddSingleton<IProgressService, SqliteProgressService>();
        services.AddSingleton<ILessonService>(sp =>
        {
            var contentDir = AppDomain.CurrentDomain.BaseDirectory;
            return new JsonLessonService(
                contentDir,
                sp.GetRequiredService<IProgressService>(),
                sp.GetRequiredService<ILogger<JsonLessonService>>());
        });
        services.AddSingleton<IPuzzleService>(sp =>
        {
            var contentDir = AppDomain.CurrentDomain.BaseDirectory;
            return new JsonPuzzleService(
                contentDir,
                sp.GetRequiredService<IProgressService>(),
                sp.GetRequiredService<ILogger<JsonPuzzleService>>());
        });
        services.AddSingleton<KnightLabChess.Core.Openings.IOpeningService>(sp =>
        {
            var contentDir = AppDomain.CurrentDomain.BaseDirectory;
            return new JsonOpeningService(
                contentDir,
                sp.GetRequiredService<ILogger<JsonOpeningService>>());
        });
        services.AddSingleton<KnightLabChess.Core.Settings.ISettingsService>(sp =>
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(appData, "KnightLabChess");
            var dbPath = Path.Combine(folder, "knightlab.db");
            return new JsonSettingsService(
                folder,
                dbPath,
                sp.GetRequiredService<ILogger<JsonSettingsService>>());
        });

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<PlayViewModel>();
        services.AddTransient<LearnViewModel>();
        services.AddTransient<PuzzlesViewModel>();
        services.AddTransient<AnalysisViewModel>();
        services.AddTransient<OpeningExplorerViewModel>();
        services.AddTransient<ProgressViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<ChessBoardViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
