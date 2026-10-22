using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnightLabChess.Core.Audio;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Coach;
using KnightLabChess.Core.Engine;
using KnightLabChess.Core.Models;
using KnightLabChess.Core.Persistence;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// ViewModel for the Play Arena page (Human vs Human, Human vs Computer/Stockfish, AI Coach, and Game Persistence).
/// </summary>
public partial class PlayViewModel : PageViewModelBase
{
    private readonly IGameRepository _gameRepository;
    private readonly IEngineService _engineService;
    private readonly IAudioService? _audioService;
    private readonly IAiCoachService? _coachService;
    private CancellationTokenSource? _coachCts;
    private CancellationTokenSource? _engineCts;

    [ObservableProperty]
    private ChessBoardViewModel _board;

    [ObservableProperty]
    private bool _isCoachEnabled = true;

    [ObservableProperty]
    private bool _isCoachAnalyzing;

    [ObservableProperty]
    private CoachResponse? _latestCoachFeedback;

    [ObservableProperty]
    private bool _hasCoachFeedback;

    [ObservableProperty]
    private string _coachStatusText = "AI Coach ready";

    [ObservableProperty]
    private string _askCoachButtonText = "✦ Ask AI Coach";

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

    public PlayViewModel(
        IGameRepository gameRepository,
        IEngineService engineService,
        IAudioService? audioService = null,
        IAiCoachService? coachService = null)
    {
        _gameRepository = gameRepository;
        _engineService = engineService;
        _audioService = audioService;
        _coachService = coachService;
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

    [RelayCommand]
    public void DismissCoachFeedback()
    {
        HasCoachFeedback = false;
        LatestCoachFeedback = null;
    }

    [RelayCommand]
    public async Task RequestCoachAdviceAsync()
    {
        if (_coachService is null || Board.IsGameOver || IsCoachAnalyzing) return;

        // When clicked before moving, provide strategic advice and candidate plans for the current position
        await RequestPositionAdviceAsync();
    }

    /// <summary>
    /// Evaluates the current position on the board to provide proactive advice, evaluation, and candidate moves.
    /// </summary>
    private async Task RequestPositionAdviceAsync()
    {
        if (_coachService is null) return;

        _coachCts?.Cancel();
        _coachCts = new CancellationTokenSource();
        var ct = _coachCts.Token;

        IsCoachAnalyzing = true;
        AskCoachButtonText = "⏳ Analyzing...";
        CoachStatusText = "AI Coach evaluating position...";
        try
        {
            var eval = await _engineService.EvaluatePositionAsync(Board.CurrentFen, depth: 12, timeoutMs: 1400, cancellationToken: ct);
            if (ct.IsCancellationRequested || eval is null) return;

            string bestMoveSan = string.Empty;
            if (!string.IsNullOrEmpty(eval.BestMoveUci))
            {
                var tempGame = new GeraChessGameAdapter(Board.CurrentFen);
                var res = tempGame.MakeMove(eval.BestMoveUci);
                if (res.Success && res.Move is not null)
                {
                    bestMoveSan = res.Move.San;
                }
            }

            var explanation = await _coachService.ExplainPositionAsync(Board.CurrentFen, eval, ct);
            if (ct.IsCancellationRequested) return;

            int scoreCp = eval.ScoreCentipawns ?? 0;
            string scoreFormatted = eval.MateInMoves.HasValue
                ? $"Mate in {Math.Abs(eval.MateInMoves.Value)} ({(eval.MateInMoves.Value > 0 ? "Advantage" : "Threatened")})"
                : $"Evaluation: {(scoreCp >= 0 ? "+" : "")}{(scoreCp / 100.0):0.00} pawns";

            string candidateAdvice = !string.IsNullOrEmpty(bestMoveSan)
                ? $"Suggested plan involves {bestMoveSan}. Prioritize piece activity and key square control."
                : "Evaluate king safety and identify unprotected pieces.";

            LatestCoachFeedback = new CoachResponse(
                Success: true,
                Classification: CoachMoveClassification.BestMove,
                Title: "Position Advice & Tactical Plan",
                Summary: scoreFormatted,
                TacticalExplanation: explanation,
                StrategicAdvice: candidateAdvice,
                KeyThreats: [],
                AlternativeIdea: !string.IsNullOrEmpty(bestMoveSan) ? $"Candidate move: {bestMoveSan}" : null
            );
            HasCoachFeedback = true;
            CoachStatusText = "AI Coach: Position advice ready";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Coach] Error evaluating position: {ex}");
            CoachStatusText = "AI Coach unavailable";
        }
        finally
        {
            IsCoachAnalyzing = false;
            AskCoachButtonText = "✦ Ask AI Coach";
        }
    }

    /// <summary>
    /// Analyzes the player's last move against the position before the move, calculating centipawn loss accurately.
    /// </summary>
    private async Task TriggerCoachAnalysisAsync(MoveRecord move)
    {
        if (_coachService is null) return;

        _coachCts?.Cancel();
        _coachCts = new CancellationTokenSource();
        var ct = _coachCts.Token;

        IsCoachAnalyzing = true;
        CoachStatusText = "AI Coach analyzing your move...";
        try
        {
            // Reconstruct the board state BEFORE the move was made
            var tempGame = new GeraChessGameAdapter();
            tempGame.LoadFromPosition(Board.Game.StartFen, Board.Game.MoveHistoryUci.Take(Board.Game.MoveHistoryUci.Count - 1));
            string fenBefore = tempGame.CurrentFen;
            string fenAfter = Board.CurrentFen;

            // Evaluate the position before the move (where it was the player's turn to move)
            var evalBefore = await _engineService.EvaluatePositionAsync(fenBefore, depth: 12, timeoutMs: 1200, cancellationToken: ct);
            if (ct.IsCancellationRequested || evalBefore is null) return;

            string? bestMoveUci = evalBefore.BestMoveUci;
            string? bestMoveSan = null;

            if (!string.IsNullOrEmpty(bestMoveUci))
            {
                var bestMoveRes = tempGame.MakeMove(bestMoveUci);
                if (bestMoveRes.Success && bestMoveRes.Move is not null)
                {
                    bestMoveSan = bestMoveRes.Move.San;
                }
            }

            // Check if player's move matched the engine's best move
            bool isBest = !string.IsNullOrEmpty(bestMoveUci) &&
                          string.Equals(move.Uci, bestMoveUci, StringComparison.OrdinalIgnoreCase);

            int scoreBefore = evalBefore.ScoreCentipawns ?? 0;
            int scoreAfter;
            int cpLoss;
            CoachMoveClassification classification;

            if (isBest)
            {
                scoreAfter = scoreBefore;
                cpLoss = 0;
                classification = CoachMoveClassification.BestMove;
            }
            else
            {
                // Evaluate position after the move (from opponent's perspective, so invert for mover)
                var evalAfter = await _engineService.EvaluatePositionAsync(fenAfter, depth: 10, timeoutMs: 1000, cancellationToken: ct);
                if (ct.IsCancellationRequested) return;

                // Invert because evalAfter is from opponent's perspective
                scoreAfter = -(evalAfter?.ScoreCentipawns ?? 0);
                cpLoss = Math.Max(0, scoreBefore - scoreAfter);

                classification = cpLoss switch
                {
                    <= 15 => CoachMoveClassification.Excellent,
                    <= 45 => CoachMoveClassification.Good,
                    <= 90 => CoachMoveClassification.Inaccuracy,
                    <= 250 => CoachMoveClassification.Mistake,
                    _ => CoachMoveClassification.Blunder
                };
            }

            // Convert principal variation UCI moves to SAN
            var pvSan = new List<string>();
            if (evalBefore.PrincipalVariation.Count > 0)
            {
                var pvGame = new GeraChessGameAdapter(fenBefore);
                foreach (var pvUci in evalBefore.PrincipalVariation.Take(5))
                {
                    var pvRes = pvGame.MakeMove(pvUci);
                    if (pvRes.Success && pvRes.Move is not null)
                    {
                        pvSan.Add(pvRes.Move.San);
                    }
                    else break;
                }
            }

            var req = new CoachAnalysisRequest(
                FenBefore: fenBefore,
                FenAfter: fenAfter,
                MoveSan: move.San,
                MoveUci: move.Uci,
                BestMoveSan: bestMoveSan ?? bestMoveUci,
                BestMoveUci: bestMoveUci,
                ScoreBeforeCp: scoreBefore,
                ScoreAfterCp: scoreAfter,
                MateIn: evalBefore.MateInMoves,
                PrincipalVariationSan: pvSan,
                Classification: classification,
                SkillLevel: "Intermediate");

            var coachResponse = await _coachService.AnalyzeMoveAsync(req, ct);
            if (!ct.IsCancellationRequested && coachResponse != null)
            {
                LatestCoachFeedback = coachResponse;
                HasCoachFeedback = true;
                CoachStatusText = $"AI Coach: {coachResponse.Title}";
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Coach] Error analyzing move: {ex}");
            CoachStatusText = "AI Coach unavailable";
        }
        finally
        {
            IsCoachAnalyzing = false;
            AskCoachButtonText = "✦ Ask AI Coach";
        }
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
        _engineCts?.Cancel();
        SelectedMode = GameMode.HumanVsHuman;
    }

    [RelayCommand]
    private async Task SetComputerMode()
    {
        SelectedMode = GameMode.HumanVsComputer;
        await CheckAndTriggerComputerMoveAsync();
    }

    [RelayCommand]
    private async Task SelectHumanColor(PieceColor color)
    {
        _engineCts?.Cancel();
        HumanColor = color;
        await CheckAndTriggerComputerMoveAsync();
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
        _engineCts?.Cancel();
        _coachCts?.Cancel();
        DismissCoachFeedback();

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

        await CheckAndTriggerComputerMoveAsync();
    }

    private async Task CheckAndTriggerComputerMoveAsync()
    {
        if (SelectedMode == GameMode.HumanVsComputer && !Board.IsGameOver && !IsEngineThinking)
        {
            var computerColor = HumanColor == PieceColor.White ? PieceColor.Black : PieceColor.White;
            if (Board.Game.ActiveTurn == computerColor)
            {
                await TriggerComputerMoveAsync();
            }
        }
    }

    private async Task TriggerComputerMoveAsync()
    {
        if (Board.IsGameOver || IsEngineThinking) return;

        _engineCts?.Cancel();
        _engineCts = new CancellationTokenSource();
        var ct = _engineCts.Token;

        IsEngineThinking = true;
        Board.IsInputDisabled = true;
        EngineThinkingText = $"{EngineName} is calculating...";

        try
        {
            await Task.Delay(200, ct);
            if (ct.IsCancellationRequested || Board.IsGameOver) return;

            var bestMove = await _engineService.GetBestMoveAsync(
                Board.CurrentFen,
                SelectedDifficulty,
                moveTimeMs: 600,
                cancellationToken: ct);

            if (!string.IsNullOrEmpty(bestMove) && !ct.IsCancellationRequested && !Board.IsGameOver)
            {
                Board.MakeMove(bestMove);
            }
        }
        catch (OperationCanceledException) { }
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
        _engineCts?.Cancel();
        _coachCts?.Cancel();

        if (SelectedMode == GameMode.HumanVsComputer)
        {
            // If human is Black and only 1 move was made (computer's opening move), undo 1 ply
            if (HumanColor == PieceColor.Black && Board.Game.MoveHistory.Count == 1)
            {
                Board.UndoMove();
            }
            else
            {
                // In computer mode, undo two plies (computer's move + human's move)
                Board.UndoMove();
                Board.UndoMove();
            }

            // Check if it is now computer's turn
            _ = CheckAndTriggerComputerMoveAsync();
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
        _engineCts?.Cancel();
        _coachCts?.Cancel();
        Board.ClaimDraw();
        _audioService?.Play(ChessSound.GameOver);
        UpdatePlayerProfiles();
    }

    [RelayCommand]
    private void Resign()
    {
        _engineCts?.Cancel();
        _coachCts?.Cancel();

        if (SelectedMode == GameMode.HumanVsComputer)
        {
            Board.Resign(HumanColor);
        }
        else
        {
            Board.Resign();
        }
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
            _engineCts?.Cancel();
            _coachCts?.Cancel();
            DismissCoachFeedback();

            var moves = JsonSerializer.Deserialize<List<string>>(saved.MoveHistoryJson) ?? [];
            Board.LoadGame(saved.StartFen, moves);

            bool isComputerGame = saved.WhitePlayer.Contains("Stockfish", StringComparison.OrdinalIgnoreCase) ||
                                  saved.BlackPlayer.Contains("Stockfish", StringComparison.OrdinalIgnoreCase) ||
                                  saved.WhitePlayer.Contains("Engine", StringComparison.OrdinalIgnoreCase) ||
                                  saved.BlackPlayer.Contains("Engine", StringComparison.OrdinalIgnoreCase) ||
                                  (!string.IsNullOrEmpty(EngineName) && (saved.WhitePlayer.Contains(EngineName, StringComparison.OrdinalIgnoreCase) || saved.BlackPlayer.Contains(EngineName, StringComparison.OrdinalIgnoreCase)));

            if (isComputerGame)
            {
                SelectedMode = GameMode.HumanVsComputer;
                bool computerIsWhite = saved.WhitePlayer.Contains("Stockfish", StringComparison.OrdinalIgnoreCase) ||
                                       saved.WhitePlayer.Contains("Engine", StringComparison.OrdinalIgnoreCase) ||
                                       (!string.IsNullOrEmpty(EngineName) && saved.WhitePlayer.Contains(EngineName, StringComparison.OrdinalIgnoreCase));
                HumanColor = computerIsWhite ? PieceColor.Black : PieceColor.White;
            }
            else
            {
                SelectedMode = GameMode.HumanVsHuman;
            }

            Board.IsFlipped = (SelectedMode == GameMode.HumanVsComputer && HumanColor == PieceColor.Black);
            UpdatePlayerProfiles();
            ShowNotification($"Loaded game #{saved.Id} ({saved.WhitePlayer} vs {saved.BlackPlayer}, {saved.MoveCount} moves).");

            await CheckAndTriggerComputerMoveAsync();
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
