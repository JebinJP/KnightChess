using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    private ChessBoardViewModel _board;

    [ObservableProperty]
    private string _whitePlayerName = "White";

    [ObservableProperty]
    private string _blackPlayerName = "Black";

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

    public PlayViewModel(IGameRepository gameRepository, IEngineService engineService)
    {
        _gameRepository = gameRepository;
        _engineService = engineService;
        _board = new ChessBoardViewModel();
        PageTitle = "Play Chess";
        PageIcon = "♟";

        Board.GameEnded += (_, result) =>
        {
            ShowNotification($"Game Over: {result}");
        };

        Board.MoveMade += async (_, san) =>
        {
            if (SelectedMode == GameMode.HumanVsComputer && !Board.IsGameOver)
            {
                // Check if it's the computer's turn
                var computerColor = HumanColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
                if (Board.Game.ActiveTurn == computerColor)
                {
                    await TriggerComputerMoveAsync();
                }
            }
        };
    }

    public override async Task OnNavigatedToAsync()
    {
        await _engineService.InitializeAsync();
        EngineName = _engineService.EngineName;
    }

    partial void OnSelectedModeChanged(GameMode value)
    {
        IsComputerMode = value == GameMode.HumanVsComputer;
        UpdatePlayerNames();
    }

    partial void OnHumanColorChanged(PieceColor value)
    {
        UpdatePlayerNames();
        Board.IsFlipped = (value == PieceColor.Black);
    }

    private void UpdatePlayerNames()
    {
        if (SelectedMode == GameMode.HumanVsComputer)
        {
            if (HumanColor == PieceColor.White)
            {
                WhitePlayerName = "Human";
                BlackPlayerName = $"{EngineName} ({SelectedDifficulty})";
            }
            else
            {
                WhitePlayerName = $"{EngineName} ({SelectedDifficulty})";
                BlackPlayerName = "Human";
            }
        }
        else
        {
            WhitePlayerName = "White";
            BlackPlayerName = "Black";
        }
    }

    [RelayCommand]
    private async Task StartNewGame()
    {
        Board.NewGame();
        UpdatePlayerNames();
        Board.IsFlipped = (HumanColor == PieceColor.Black);

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
    }

    [RelayCommand]
    private void FlipBoard()
    {
        Board.FlipBoard();
    }

    [RelayCommand]
    private void ClaimDraw()
    {
        Board.ClaimDraw();
    }

    [RelayCommand]
    private void Resign()
    {
        Board.Resign();
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
            WhitePlayerName = saved.WhitePlayer;
            BlackPlayerName = saved.BlackPlayer;
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
