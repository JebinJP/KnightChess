using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnightLabChess.Core.Engine;
using KnightLabChess.Core.Settings;
using Microsoft.Win32;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// ViewModel for application preferences, Stockfish engine configuration, and visual settings.
/// </summary>
public partial class SettingsViewModel : PageViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IEngineService _engineService;

    // Engine
    [ObservableProperty] private string _stockfishPath = string.Empty;
    [ObservableProperty] private string _engineStatusText = "Active";
    [ObservableProperty] private bool _isStockfishAvailable;
    [ObservableProperty] private int _engineDepth = 15;
    [ObservableProperty] private int _engineThreads = 2;
    [ObservableProperty] private int _engineHashMb = 128;
    [ObservableProperty] private bool _useFallbackIfNotFound = true;

    // Visuals & Board
    [ObservableProperty] private string _selectedBoardTheme = "Emerald";
    [ObservableProperty] private bool _highlightLegalMoves = true;
    [ObservableProperty] private bool _showMoveAnimations = true;
    [ObservableProperty] private bool _soundEnabled = true;
    [ObservableProperty] private int _soundVolume = 80;

    // Gameplay
    [ObservableProperty] private bool _autoQueenPromotion;
    [ObservableProperty] private bool _showEngineEvalDuringPlay;

    // System
    [ObservableProperty] private string _databasePath = string.Empty;
    [ObservableProperty] private string _appVersion = "1.0.0";
    [ObservableProperty] private string? _feedbackMessage;

    public string[] BoardThemeOptions { get; } = ["Emerald", "Classic Wood", "Midnight", "Ocean"];

    public SettingsViewModel(ISettingsService settingsService, IEngineService engineService)
    {
        _settingsService = settingsService;
        _engineService = engineService;

        PageTitle = "Settings";
        PageIcon = "⚙";
    }

    public override async Task OnNavigatedToAsync()
    {
        try
        {
            DatabasePath = _settingsService.GetDatabasePath();
            IsStockfishAvailable = _engineService.IsEngineAvailable;
            EngineStatusText = IsStockfishAvailable
                ? $"{_engineService.EngineName} Connected"
                : "Embedded Fallback Tactical Engine Active (Pure Offline)";

            var s = await _settingsService.GetSettingsAsync();
            StockfishPath = s.StockfishPath;
            EngineDepth = s.EngineDepth;
            EngineThreads = s.EngineThreads;
            EngineHashMb = s.EngineHashMb;
            UseFallbackIfNotFound = s.UseBuiltinFallbackIfNotFound;

            SelectedBoardTheme = s.BoardTheme;
            HighlightLegalMoves = s.HighlightLegalMoves;
            ShowMoveAnimations = s.ShowMoveAnimations;
            SoundEnabled = s.SoundEnabled;
            SoundVolume = s.SoundVolume;

            AutoQueenPromotion = s.AutoQueenPromotion;
            ShowEngineEvalDuringPlay = s.ShowEngineEvalDuringPlay;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] OnNavigatedToAsync error: {ex}");
            FeedbackMessage = "Failed to load settings.";
        }
    }

    [RelayCommand]
    public void BrowseStockfish()
    {
        var dlg = new OpenFileDialog
        {
            Title = "Select Stockfish Executable",
            Filter = "Stockfish / UCI Engine (*.exe)|*.exe|All Files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dlg.ShowDialog() == true)
        {
            StockfishPath = dlg.FileName;
            _ = TestEngineAsync();
        }
    }

    [RelayCommand]
    public async Task TestEngineAsync()
    {
        if (string.IsNullOrWhiteSpace(StockfishPath) || !File.Exists(StockfishPath))
        {
            EngineStatusText = "Specified path does not exist. Using embedded fallback engine.";
            IsStockfishAvailable = false;
            return;
        }

        try
        {
            await _engineService.InitializeAsync(StockfishPath);
            IsStockfishAvailable = _engineService.IsEngineAvailable;
            EngineStatusText = IsStockfishAvailable
                ? $"✓ {_engineService.EngineName} initialized successfully!"
                : "Engine failed to respond to UCI. Using embedded fallback.";
        }
        catch (Exception ex)
        {
            EngineStatusText = $"Engine test error: {ex.Message}";
            IsStockfishAvailable = false;
        }
    }

    [RelayCommand]
    public void SelectBoardTheme(string? theme)
    {
        if (!string.IsNullOrWhiteSpace(theme))
        {
            SelectedBoardTheme = theme;
        }
    }

    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        try
        {
            var settings = new UserSettings
            {
                StockfishPath = StockfishPath,
                EngineDepth = EngineDepth,
                EngineThreads = EngineThreads,
                EngineHashMb = EngineHashMb,
                UseBuiltinFallbackIfNotFound = UseFallbackIfNotFound,

                BoardTheme = SelectedBoardTheme,
                HighlightLegalMoves = HighlightLegalMoves,
                ShowMoveAnimations = ShowMoveAnimations,
                SoundEnabled = SoundEnabled,
                SoundVolume = SoundVolume,

                AutoQueenPromotion = AutoQueenPromotion,
                ShowEngineEvalDuringPlay = ShowEngineEvalDuringPlay
            };

            await _settingsService.SaveSettingsAsync(settings);

            if (!string.IsNullOrWhiteSpace(StockfishPath) && File.Exists(StockfishPath))
            {
                try
                {
                    await _engineService.InitializeAsync(StockfishPath);
                    IsStockfishAvailable = _engineService.IsEngineAvailable;
                    EngineStatusText = IsStockfishAvailable
                        ? $"{_engineService.EngineName} Connected"
                        : "Engine failed to initialize. Embedded fallback active.";
                }
                catch (Exception engineEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[Settings] Engine re-init error: {engineEx}");
                    EngineStatusText = "Engine initialization failed. Embedded fallback active.";
                }
            }

            FeedbackMessage = "✓ Settings saved successfully!";

            await Task.Delay(3000);
            FeedbackMessage = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] SaveSettingsAsync error: {ex}");
            FeedbackMessage = $"Failed to save settings: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ResetToDefaultsAsync()
    {
        try
        {
            var defaults = new UserSettings();
            StockfishPath = defaults.StockfishPath;
            EngineDepth = defaults.EngineDepth;
            EngineThreads = defaults.EngineThreads;
            EngineHashMb = defaults.EngineHashMb;
            UseFallbackIfNotFound = defaults.UseBuiltinFallbackIfNotFound;

            SelectedBoardTheme = defaults.BoardTheme;
            HighlightLegalMoves = defaults.HighlightLegalMoves;
            ShowMoveAnimations = defaults.ShowMoveAnimations;
            SoundEnabled = defaults.SoundEnabled;
            SoundVolume = defaults.SoundVolume;

            AutoQueenPromotion = defaults.AutoQueenPromotion;
            ShowEngineEvalDuringPlay = defaults.ShowEngineEvalDuringPlay;

            await _settingsService.SaveSettingsAsync(defaults);
            FeedbackMessage = "Settings restored to defaults.";

            await Task.Delay(3000);
            FeedbackMessage = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] ResetToDefaultsAsync error: {ex}");
            FeedbackMessage = $"Failed to reset settings: {ex.Message}";
        }
    }
}
