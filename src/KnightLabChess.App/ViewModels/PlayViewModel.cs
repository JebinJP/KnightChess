using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnightLabChess.Core.Audio;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Engine;
using KnightLabChess.Core.Models;
using KnightLabChess.Core.Persistence;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// ViewModel for the Play Arena page (Human vs Human, Human vs Computer/Stockfish, and Game Persistence).
/// </summary>
public partial class PlayViewModel : PageViewModelBase
{
    private readonly IGameRepository _gameRepository;
    private readonly IEngineService _engineService;
    private readonly IAudioService? _audioService;

    [ObservableProperty]
    private ChessBoardViewModel _board;

    [ObservableProperty]
    private string _whitePlayerName = "White";

    [ObservableProperty]
    private string _blackPlayerName = "Black";

    [ObservableProperty]
    private string _topPlayerName = "Black";

    [ObservableProperty]
    private string _bottomPlayerName = "White";

    [ObservableProperty]
    private string _topPlayerSubtext = "Player 2";

    [ObservableProperty]
    private string _bottomPlayerSubtext = "Player 1";

    [ObservableProperty]
    private string _topPlayerIcon = "♟";

    [ObservableProperty]
    private string _bottomPlayerIcon = "♙";

    [ObservableProperty]
    private bool _isTopPlayerActiveTurn;

    [ObservableProperty]
    private bool _isBottomPlayerActiveTurn = true;

    [ObservableProperty]
    private string _notificationMessage = string.Empty;

    [ObservableProperty]
    private bool _hasNotification;

    [ObservableProperty]
    private GameMode _selectedMode = GameMode.HumanVsHuman;

    [ObservableProperty]
    private bool _isComputerMode;

    [ObservableProperty]
    private EngineDifficulty _selectedDifficulty = EngineDifficulty.Casual;

    [ObservableProperty]
    private PieceColor _humanColor = PieceColor.White;

    [ObservableProperty]
    private bool _isEngineThinking;

    [ObservableProperty]
    private string _engineThinkingText = "Computer thinking...";

    [ObservableProperty]
    private string _engineName = "Stockfish";

    public GameMode[] GameModes { get; } = Enum.GetValues<GameMode>();
    public EngineDifficulty[] Difficulties { get; } = Enum.GetValues<EngineDifficulty>();

    public bool IsHumanVsHuman => SelectedMode == GameMode.HumanVsHuman;
    public bool IsHumanVsComputer => SelectedMode == GameMode.HumanVsComputer;
    public bool IsHumanWhite => HumanColor == PieceColor.White;
    public bool IsHumanBlack => HumanColor == PieceColor.Black;
    public bool IsCasualDifficulty => SelectedDifficulty == EngineDifficulty.Casual;
    public bool IsIntermediateDifficulty => SelectedDifficulty == EngineDifficulty.Intermediate;
    public bool IsAdvancedDifficulty => SelectedDifficulty == EngineDifficulty.Advanced;
    public bool IsMasterDifficulty => SelectedDifficulty == EngineDifficulty.Master;

    public PlayViewModel(IGameRepository gameRepository, IEngineService engineService, IAudioService? audioService = null)
    {
        _gameRepository = gameRepository;
        _engineService = engineService;
        _audioService = audioService;
        _board = new ChessBoardViewModel(audioService);
        PageTitle = "Play Chess";
        PageIcon = "♟";

        Board.GameEnded += (_, result) =>
        {
            UpdatePlayerProfiles();
            ShowNotification($"Game Over: {result}");
        };

        Board.MoveMade += async (_, san) =>
        {
            UpdatePlayerProfiles();

            if (SelectedMode == GameMode.HumanVsComputer && !Board.IsGameOver)
            {
                var computerColor = HumanColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
                if (Board.Game.ActiveTurn == computerColor)
                {
                    await TriggerComputerMoveAsync();
                }
            }
        };

        UpdatePlayerProfiles();
    }

    public override async Task OnNavigatedToAsync()
    {
        await _engineService.InitializeAsync();
        EngineName = _engineService.EngineName;
        UpdatePlayerProfiles();
    }

    partial void OnSelectedModeChanged(GameMode value)
    {
        IsComputerMode = value == GameMode.HumanVsComputer;
        if (value == GameMode.HumanVsComputer)
        {
            Board.IsFlipped = (HumanColor == PieceColor.Black);
        }
        NotifyStateChanged();
        UpdatePlayerProfiles();
    }

    partial void OnHumanColorChanged(PieceColor value)
    {
        if (SelectedMode == GameMode.HumanVsComputer)
        {
            Board.IsFlipped = (value == PieceColor.Black);
        }
        NotifyStateChanged();
        UpdatePlayerProfiles();
    }

    partial void OnSelectedDifficultyChanged(EngineDifficulty value)
    {
        NotifyStateChanged();
        UpdatePlayerProfiles();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(IsHumanVsHuman));
        OnPropertyChanged(nameof(IsHumanVsComputer));
        OnPropertyChanged(nameof(IsHumanWhite));
        OnPropertyChanged(nameof(IsHumanBlack));
        OnPropertyChanged(nameof(IsCasualDifficulty));
        OnPropertyChanged(nameof(IsIntermediateDifficulty));
        OnPropertyChanged(nameof(IsAdvancedDifficulty));
        OnPropertyChanged(nameof(IsMasterDifficulty));
    }

    [RelayCommand]
    private void SelectGameMode(GameMode mode)
    {
        SelectedMode = mode;
    }

    [RelayCommand]
    private void SetPassAndPlayMode()
    {
        SelectedMode = GameMode.HumanVsHuman;
    }

    [RelayCommand]
    private void SetComputerMode()
    {
        SelectedMode = GameMode.HumanVsComputer;
    }

    [RelayCommand]
    private async Task SelectHumanColor(PieceColor color)
    {
        HumanColor = color;
        // If switching color in a fresh game where computer is white, trigger first move
        if (SelectedMode == GameMode.HumanVsComputer &&
            HumanColor == PieceColor.Black &&
            Board.Game.MoveHistory.Count == 0 &&
            !Board.IsGameOver)
        {
            await TriggerComputerMoveAsync();
        }
    }

    [RelayCommand]
    private async Task SetWhiteColor()
    {
        await SelectHumanColor(PieceColor.White);
    }

    [RelayCommand]
    private async Task SetBlackColor()
    {
        await SelectHumanColor(PieceColor.Black);
    }

    [RelayCommand]
    private void SelectDifficulty(EngineDifficulty difficulty)
    {
        SelectedDifficulty = difficulty;
    }

    [RelayCommand]
    private void SetCasualDifficulty() => SelectedDifficulty = EngineDifficulty.Casual;

    [RelayCommand]
    private void SetIntermediateDifficulty() => SelectedDifficulty = EngineDifficulty.Intermediate;

    [RelayCommand]
    private void SetAdvancedDifficulty() => SelectedDifficulty = EngineDifficulty.Advanced;

    [RelayCommand]
    private void SetMasterDifficulty() => SelectedDifficulty = EngineDifficulty.Master;

    private void UpdatePlayerProfiles()
    {
        bool isFlipped = Board.IsFlipped;
        bool isWhiteTurn = Board.IsWhiteTurn;

        if (SelectedMode == GameMode.HumanVsComputer)
        {
            if (HumanColor == PieceColor.White)
            {
                WhitePlayerName = "Human";
                BlackPlayerName = $"{EngineName} ({SelectedDifficulty})";

                if (!isFlipped)
                {
                    TopPlayerName = BlackPlayerName;
                    TopPlayerSubtext = $"Engine • {SelectedDifficulty}";
                    TopPlayerIcon = "🤖";
                    BottomPlayerName = "You";
                    BottomPlayerSubtext = "White";
                    BottomPlayerIcon = "👤";
                    IsTopPlayerActiveTurn = !isWhiteTurn && !Board.IsGameOver;
                    IsBottomPlayerActiveTurn = isWhiteTurn && !Board.IsGameOver;
                }
                else
                {
                    TopPlayerName = "You";
                    TopPlayerSubtext = "White";
                    TopPlayerIcon = "👤";
                    BottomPlayerName = BlackPlayerName;
                    BottomPlayerSubtext = $"Engine • {SelectedDifficulty}";
                    BottomPlayerIcon = "🤖";
                    IsTopPlayerActiveTurn = isWhiteTurn && !Board.IsGameOver;
                    IsBottomPlayerActiveTurn = !isWhiteTurn && !Board.IsGameOver;
                }
            }
            else // Human is Black
            {
                WhitePlayerName = $"{EngineName} ({SelectedDifficulty})";
                BlackPlayerName = "Human";

                if (isFlipped)
                {
                    TopPlayerName = WhitePlayerName;
                    TopPlayerSubtext = $"Engine • {SelectedDifficulty}";
                    TopPlayerIcon = "🤖";
                    BottomPlayerName = "You";
                    BottomPlayerSubtext = "Black";
                    BottomPlayerIcon = "👤";
                    IsTopPlayerActiveTurn = isWhiteTurn && !Board.IsGameOver;
                    IsBottomPlayerActiveTurn = !isWhiteTurn && !Board.IsGameOver;
                }
                else
                {
                    TopPlayerName = "You";
                    TopPlayerSubtext = "Black";
                    TopPlayerIcon = "👤";
                    BottomPlayerName = WhitePlayerName;
                    BottomPlayerSubtext = $"Engine • {SelectedDifficulty}";
                    BottomPlayerIcon = "🤖";
                    IsTopPlayerActiveTurn = !isWhiteTurn && !Board.IsGameOver;
                    IsBottomPlayerActiveTurn = isWhiteTurn && !Board.IsGameOver;
                }
            }
        }
        else // Human vs Human (Pass & Play)
        {
            WhitePlayerName = "White";
            BlackPlayerName = "Black";

            if (!isFlipped)
            {
                TopPlayerName = "Black";
                TopPlayerSubtext = "Player 2";
                TopPlayerIcon = "♟";
                BottomPlayerName = "White";
                BottomPlayerSubtext = "Player 1";
                BottomPlayerIcon = "♙";
                IsTopPlayerActiveTurn = !isWhiteTurn && !Board.IsGameOver;
                IsBottomPlayerActiveTurn = isWhiteTurn && !Board.IsGameOver;
            }
            else
            {
                TopPlayerName = "White";
                TopPlayerSubtext = "Player 1";
                TopPlayerIcon = "♙";
                BottomPlayerName = "Black";
                BottomPlayerSubtext = "Player 2";
                BottomPlayerIcon = "♟";
                IsTopPlayerActiveTurn = isWhiteTurn && !Board.IsGameOver;
                IsBottomPlayerActiveTurn = !isWhiteTurn && !Board.IsGameOver;
            }
        }
    }

    [RelayCommand]
    private async Task StartNewGame()
    {
        Board.NewGame();
        if (SelectedMode == GameMode.HumanVsComputer)
        {
            Board.IsFlipped = (HumanColor == PieceColor.Black);
        }
        else
        {
            Board.IsFlipped = false;
        }

        UpdatePlayerProfiles();
        ShowNotification($"New {SelectedMode} game started. White to move.");

        // If computer plays white, trigger its opening move
        if (SelectedMode == GameMode.HumanVsComputer && HumanColor == PieceColor.Black)
        {
            await TriggerComputerMoveAsync();
        }
    }

    private async Task TriggerComputerMoveAsync()
    {
        if (Board.IsGameOver || IsEngineThinking) return;

        IsEngineThinking = true;
        Board.IsInputDisabled = true;
        EngineThinkingText = $"{EngineName} is calculating...";

        try
        {
            // Allow brief UI update before engine calculates
            await Task.Delay(200);

            var bestMove = await _engineService.GetBestMoveAsync(
                Board.CurrentFen,
                SelectedDifficulty,
                moveTimeMs: 600);

            if (!string.IsNullOrEmpty(bestMove))
            {
                Board.MakeMove(bestMove);
            }
        }
        catch (Exception ex)
        {
            ShowNotification($"Engine error: {ex.Message}");
        }
        finally
        {
            IsEngineThinking = false;
            Board.IsInputDisabled = false;
            UpdatePlayerProfiles();
        }
    }

    [RelayCommand]
    private void UndoLastMove()
    {
        if (SelectedMode == GameMode.HumanVsComputer)
        {
            // In computer mode, undo two plies (computer's move + human's move)
            Board.UndoMove();
            Board.UndoMove();
        }
        else
        {
            Board.UndoMove();
        }
        UpdatePlayerProfiles();
    }

    [RelayCommand]
    private void FlipBoard()
    {
        Board.FlipBoard();
        UpdatePlayerProfiles();
    }

    [RelayCommand]
    private void ClaimDraw()
    {
        Board.ClaimDraw();
        _audioService?.Play(ChessSound.GameOver);
        UpdatePlayerProfiles();
    }

    [RelayCommand]
    private void Resign()
    {
        Board.Resign();
        _audioService?.Play(ChessSound.GameOver);
        UpdatePlayerProfiles();
    }

    [RelayCommand]
    private async Task SaveActiveGame()
    {
        var game = Board.Game;
        var record = new GameRecord
        {
            WhitePlayer = WhitePlayerName,
            BlackPlayer = BlackPlayerName,
            Result = game.Result,
            EndReason = game.EndReason,
            StartFen = game.StartFen,
            CurrentFen = game.CurrentFen,
            Pgn = game.GetPgn(WhitePlayerName, BlackPlayerName),
            MoveHistoryJson = JsonSerializer.Serialize(game.MoveHistoryUci),
            MoveCount = game.MoveHistory.Count,
            CompletedAt = game.IsGameOver ? DateTime.UtcNow : null
        };

        int savedId = await _gameRepository.SaveGameAsync(record);
        ShowNotification($"Game saved successfully! (ID: {savedId})");
    }

    [RelayCommand]
    private async Task LoadLastSavedGame()
    {
        var recent = await _gameRepository.GetRecentGamesAsync(limit: 1);
        if (recent.Count == 0)
        {
            ShowNotification("No saved games found in database.");
            return;
        }

        var saved = recent[0];
        try
        {
            var moves = JsonSerializer.Deserialize<List<string>>(saved.MoveHistoryJson) ?? [];
            Board.LoadGame(saved.StartFen, moves);

            if (saved.WhitePlayer.Contains("Stockfish") || saved.BlackPlayer.Contains("Stockfish"))
            {
                SelectedMode = GameMode.HumanVsComputer;
                HumanColor = saved.WhitePlayer.Contains("Stockfish") ? PieceColor.Black : PieceColor.White;
            }
            else
            {
                SelectedMode = GameMode.HumanVsHuman;
            }

            Board.IsFlipped = (SelectedMode == GameMode.HumanVsComputer && HumanColor == PieceColor.Black);
            UpdatePlayerProfiles();
            ShowNotification($"Loaded game #{saved.Id} ({saved.WhitePlayer} vs {saved.BlackPlayer}, {saved.MoveCount} moves).");
        }
        catch (Exception ex)
        {
            ShowNotification($"Failed to load game: {ex.Message}");
        }
    }

    private void ShowNotification(string msg)
    {
        NotificationMessage = msg;
        HasNotification = true;
    }

    [RelayCommand]
    private void DismissNotification()
    {
        HasNotification = false;
        NotificationMessage = string.Empty;
    }
}
