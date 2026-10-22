using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnightLabChess.Core.Audio;
using KnightLabChess.Core.Coach;
using KnightLabChess.Core.Engine;
using KnightLabChess.Core.Settings;
using Microsoft.Win32;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// ViewModel for application preferences, Stockfish engine configuration, Gemini AI Coach, and visual settings.
/// </summary>
public partial class SettingsViewModel : PageViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IEngineService _engineService;
    private readonly IAudioService? _audioService;
    private readonly IAiCoachService? _coachService;

    // Engine
    [ObservableProperty] private string _stockfishPath = string.Empty;
    [ObservableProperty] private string _engineStatusText = "Active";
    [ObservableProperty] private bool _isStockfishAvailable;
    [ObservableProperty] private int _engineDepth = 15;
    [ObservableProperty] private int _engineThreads = 2;
    [ObservableProperty] private int _engineHashMb = 128;
    [ObservableProperty] private bool _useFallbackIfNotFound = true;

    // AI Chess Coach (Gemini)
    [ObservableProperty] private string _geminiApiKey = string.Empty;
    [ObservableProperty] private string _geminiModel = "gemini-2.5-flash";
    [ObservableProperty] private bool _enableAiCoach = true;
    [ObservableProperty] private string _coachPersona = "Encouraging Grandmaster";
    [ObservableProperty] private string _coachSkillLevel = "Intermediate";
    [ObservableProperty] private string _coachStatusText = "Ready";

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
    [ObservableProperty] private string _appVersion = "2.0.0";
    [ObservableProperty] private string? _feedbackMessage;

    public string[] BoardThemeOptions { get; } = ["Emerald", "Classic Wood", "Midnight", "Ocean"];
    public string[] CoachModelOptions { get; } = ["gemini-2.5-flash", "gemini-2.5-pro", "gemini-2.0-flash"];
    public string[] CoachPersonaOptions { get; } = ["Encouraging Grandmaster", "Strict Tournament Director", "Tactical Speed Coach"];
    public string[] CoachSkillLevelOptions { get; } = ["Beginner", "Intermediate", "Advanced"];

    public SettingsViewModel(
        ISettingsService settingsService,
        IEngineService engineService,
        IAudioService? audioService = null,
        IAiCoachService? coachService = null)
    {
        _settingsService = settingsService;
        _engineService = engineService;
        _audioService = audioService;
        _coachService = coachService;

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

            GeminiApiKey = s.GeminiApiKey;
            GeminiModel = s.GeminiModel;
            EnableAiCoach = s.EnableAiCoach;
            CoachPersona = s.CoachPersona;
            CoachSkillLevel = s.CoachSkillLevel;
            CoachStatusText = !string.IsNullOrWhiteSpace(GeminiApiKey)
                ? "✓ Gemini AI Coach configured & active"
                : "Pure Offline Heuristic Coach active (Enter API Key for Gemini)";

            SelectedBoardTheme = s.BoardTheme;
            HighlightLegalMoves = s.HighlightLegalMoves;
            ShowMoveAnimations = s.ShowMoveAnimations;
            SoundEnabled = s.SoundEnabled;
            SoundVolume = s.SoundVolume;

            _audioService?.UpdateSettings(SoundEnabled, SoundVolume);

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
    public void TestAudio()
    {
        _audioService?.UpdateSettings(SoundEnabled, SoundVolume);
        _audioService?.Play(ChessSound.Move);
    }

    [RelayCommand]
    public async Task TestCoachAsync()
    {
        CoachStatusText = "Testing coach service...";
        try
        {
            if (_coachService is null)
            {
                CoachStatusText = "AI Coach service unavailable.";
                return;
            }

            var sampleReq = new CoachAnalysisRequest(
                FenBefore: "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1",
                FenAfter: "rnbqkbnr/pppp1ppp/8/4p3/4P3/8/PPPP1PPP/RNBQKBNR w KQkq e6 0 2",
                MoveSan: "e5",
                MoveUci: "e7e5",
                BestMoveSan: "e5",
                BestMoveUci: "e7e5",
                ScoreBeforeCp: 20,
                ScoreAfterCp: 15,
                MateIn: null,
                PrincipalVariationSan: ["Nf3", "Nc6", "Bb5"],
                Classification: CoachMoveClassification.BestMove,
                SkillLevel: CoachSkillLevel);

            var res = await _coachService.AnalyzeMoveAsync(sampleReq);
            CoachStatusText = res.Success
                ? $"✓ Coach Ready: {res.Summary}"
                : $"Coach Warning: {res.ErrorMessage ?? "Fallback active"}";
        }
        catch (Exception ex)
        {
            CoachStatusText = $"Coach test error: {ex.Message}";
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

                GeminiApiKey = GeminiApiKey,
                GeminiModel = GeminiModel,
                EnableAiCoach = EnableAiCoach,
                CoachPersona = CoachPersona,
                CoachSkillLevel = CoachSkillLevel,

                BoardTheme = SelectedBoardTheme,
                HighlightLegalMoves = HighlightLegalMoves,
                ShowMoveAnimations = ShowMoveAnimations,
                SoundEnabled = SoundEnabled,
                SoundVolume = SoundVolume,

                AutoQueenPromotion = AutoQueenPromotion,
                ShowEngineEvalDuringPlay = ShowEngineEvalDuringPlay
            };

            await _settingsService.SaveSettingsAsync(settings);
            _audioService?.UpdateSettings(SoundEnabled, SoundVolume);

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

            GeminiApiKey = defaults.GeminiApiKey;
            GeminiModel = defaults.GeminiModel;
            EnableAiCoach = defaults.EnableAiCoach;
            CoachPersona = defaults.CoachPersona;
            CoachSkillLevel = defaults.CoachSkillLevel;

            SelectedBoardTheme = defaults.BoardTheme;
            HighlightLegalMoves = defaults.HighlightLegalMoves;
            ShowMoveAnimations = defaults.ShowMoveAnimations;
            SoundEnabled = defaults.SoundEnabled;
            SoundVolume = defaults.SoundVolume;

            AutoQueenPromotion = defaults.AutoQueenPromotion;
            ShowEngineEvalDuringPlay = defaults.ShowEngineEvalDuringPlay;

            await _settingsService.SaveSettingsAsync(defaults);
            _audioService?.UpdateSettings(SoundEnabled, SoundVolume);

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
