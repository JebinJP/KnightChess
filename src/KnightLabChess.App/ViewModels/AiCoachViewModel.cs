using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnightLabChess.Core.Audio;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Coach;
using KnightLabChess.Core.Engine;
using KnightLabChess.Core.Lessons;
using KnightLabChess.Core.Models;
using KnightLabChess.Core.Puzzles;
using Microsoft.Extensions.DependencyInjection;

namespace KnightLabChess.App.ViewModels;

public record LessonRecommendationItem(string Id, string Title, string Description, string Stage);
public record PuzzleRecommendationItem(string Id, string Theme, int Rating, string Description);

public partial class ChatMessageItem : ObservableObject
{
    public string Sender { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public string? EngineEvidence { get; init; }
    public bool IsLegalMove { get; init; } = true;
    public string? ValidatedMoveSan { get; init; }
    public bool IsCoach { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

/// <summary>
/// Dedicated AI Chess Coach ViewModel conforming to KnightLab V2.0 Design System.
/// Features move explanations, threat radar, progressive hints, rules-grounded Q&amp;A, and post-game reviews.
/// </summary>
public partial class AiCoachViewModel : PageViewModelBase
{
    private readonly IAiCoachService _coachService;
    private readonly IEngineService _engineService;
    private readonly ILessonService _lessonService;
    private readonly IPuzzleService _puzzleService;
    private readonly IAudioService? _audioService;
    private readonly IServiceProvider _serviceProvider;
    private CancellationTokenSource? _activeCts;

    [ObservableProperty] private ChessBoardViewModel _board;
    [ObservableProperty] private string _selectedTab = "MoveAnalysis"; // MoveAnalysis, Threats, Hints, QandA, GameReview
    [ObservableProperty] private string _selectedSkillLevel = "Intermediate";
    [ObservableProperty] private string _selectedPersona = "Grandmaster Mentor";

    [RelayCommand]
    public void ChangeTab(string tab) => SelectedTab = tab;
    [ObservableProperty] private bool _isThinking;
    [ObservableProperty] private string _statusMessage = "Ready for coaching.";
    [ObservableProperty] private bool _isAiOnline;
    [ObservableProperty] private string _aiStatusBadge = "Gemini AI Connected";

    // Engine Grounding Metadata
    [ObservableProperty] private string _engineEvalText = "+0.0";
    [ObservableProperty] private string _bestMoveText = "None";
    [ObservableProperty] private int _evalDepth = 14;
    [ObservableProperty] private EngineEvaluation? _currentEvaluation;

    // Move Explanation Tab
    [ObservableProperty] private CoachResponse? _lastMoveResponse;
    [ObservableProperty] private bool _hasMoveExplanation;
    [ObservableProperty] private string _moveClassificationBadge = "Analysis Pending";
    [ObservableProperty] private string _moveClassificationBrush = "AccentBrush";
    [ObservableProperty] private string _moveExplanationTitle = string.Empty;
    [ObservableProperty] private string _moveExplanationSummary = "Make a move on the board or click 'Explain Last Move' to receive grandmaster feedback.";
    [ObservableProperty] private string _moveExplanationTactics = string.Empty;
    [ObservableProperty] private string _moveExplanationStrategy = string.Empty;
    [ObservableProperty] private string _moveScoreBefore = "-";
    [ObservableProperty] private string _moveScoreAfter = "-";
    [ObservableProperty] private string _moveScoreDelta = "-";
    [ObservableProperty] private string _moveBestAlternative = "-";
    [ObservableProperty] private ObservableCollection<string> _moveKeyThreats = [];

    // Opponent Threat Radar Tab
    [ObservableProperty] private CoachThreatResponse? _threatResponse;
    [ObservableProperty] private bool _hasThreatAnalysis;
    [ObservableProperty] private string _threatSummary = "Click 'Scan Opponent Threats' to analyze tactical threats and candidate counters.";
    [ObservableProperty] private ObservableCollection<string> _directThreats = [];
    [ObservableProperty] private ObservableCollection<string> _defensiveIdeas = [];
    [ObservableProperty] private string? _primaryThreatSquare;

    // Progressive Hint Ladder Tab
    [ObservableProperty] private int _hintLevel;
    [ObservableProperty] private string _hintLevelBadge = "No Hint Requested";
    [ObservableProperty] private string _hintTitle = "Progressive Hints";
    [ObservableProperty] private string _hintDescription = "Stuck in a position? Request progressive hints from subtle directional nudges to full tactical lines without spoiling the puzzle.";
    [ObservableProperty] private string? _hintTargetSquare;
    [ObservableProperty] private bool _canRequestNextHint = true;

    // Interactive Q&A Tab
    [ObservableProperty] private string _questionInput = string.Empty;
    [ObservableProperty] private ObservableCollection<ChatMessageItem> _chatMessages = [];

    // Game Review Tab
    [ObservableProperty] private string _customPgnInput = string.Empty;
    [ObservableProperty] private bool _hasGameReview;
    [ObservableProperty] private int _whiteAccuracy = 50;
    [ObservableProperty] private int _blackAccuracy = 50;
    [ObservableProperty] private string _gameReviewSummary = "Play through a game on the board, paste a PGN, or click 'Review Current Game' for full post-game analysis.";
    [ObservableProperty] private ObservableCollection<TurningPointItem> _turningPoints = [];
    [ObservableProperty] private TurningPointItem? _selectedTurningPoint;
    [ObservableProperty] private ObservableCollection<string> _keyTakeaways = [];
    [ObservableProperty] private ObservableCollection<LessonRecommendationItem> _recommendedLessons = [];
    [ObservableProperty] private ObservableCollection<PuzzleRecommendationItem> _recommendedPuzzles = [];

    public string[] AvailableSkillLevels { get; } = ["Beginner", "Intermediate", "Advanced"];
    public string[] AvailablePersonas { get; } = ["Grandmaster Mentor", "Tactical Drill Instructor", "Tournament Coach"];

    public AiCoachViewModel(
        IAiCoachService coachService,
        IEngineService engineService,
        ILessonService lessonService,
        IPuzzleService puzzleService,
        IServiceProvider serviceProvider,
        IAudioService? audioService = null)
    {
        _coachService = coachService;
        _engineService = engineService;
        _lessonService = lessonService;
        _puzzleService = puzzleService;
        _serviceProvider = serviceProvider;
        _audioService = audioService;

        PageTitle = "AI Coach";
        PageIcon = "✦";

        _board = new ChessBoardViewModel(audioService);
        _board.MoveMade += async (_, san) =>
        {
            await OnBoardMoveMadeAsync(san);
        };

        UpdateAiStatus();
        AddWelcomeMessage();
    }

    private void UpdateAiStatus()
    {
        IsAiOnline = _coachService.IsConfigured;
        AiStatusBadge = IsAiOnline ? "Gemini 2.5 Flash Online" : "Tactical Offline Engine";
    }

    private void AddWelcomeMessage()
    {
        ChatMessages.Add(new ChatMessageItem
        {
            Sender = "Gemini AI Coach",
            Text = "Welcome to the KnightLab Coaching Arena. Ask me anything about your current position, plans, piece placement, or candidate moves.",
            EngineEvidence = "Engine: Authoritative Stockfish & Gera Rules Verification Active",
            IsCoach = true
        });
    }

    public override async Task OnNavigatedToAsync()
    {
        UpdateAiStatus();
        await _engineService.InitializeAsync();
        await RunEngineEvaluationAsync();
    }

    public override async Task OnNavigatedFromAsync()
    {
        _activeCts?.Cancel();
        await _engineService.StopAsync();
    }

    // =========================================================================
    // Contextual Loading API (used by PlayView / AnalysisView)
    // =========================================================================

    public async Task LoadPositionForCoachingAsync(string fen, IReadOnlyList<string>? moveHistory = null)
    {
        try
        {
            Board.LoadFen(fen);
            ResetHints();
            HasMoveExplanation = false;
            HasThreatAnalysis = false;
            await RunEngineEvaluationAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load position: {ex.Message}";
        }
    }

    // =========================================================================
    // Board Interaction & Evaluation
    // =========================================================================

    private async Task OnBoardMoveMadeAsync(string san)
    {
        ResetHints();
        await RunEngineEvaluationAsync();

        // If currently in Move Analysis tab, automatically provide move analysis
        if (SelectedTab == "MoveAnalysis")
        {
            await ExplainLastMoveAsync();
        }
        else if (SelectedTab == "Threats")
        {
            await ScanOpponentThreatsAsync();
        }
    }

    private async Task RunEngineEvaluationAsync()
    {
        _activeCts?.Cancel();
        _activeCts = new CancellationTokenSource();
        var ct = _activeCts.Token;

        try
        {
            StatusMessage = "Analyzing with Stockfish...";
            var eval = await _engineService.EvaluatePositionAsync(Board.CurrentFen, depth: 14, timeoutMs: 1200, cancellationToken: ct);
            if (eval is null || ct.IsCancellationRequested) return;

            CurrentEvaluation = eval;
            EvalDepth = eval.Depth;
            BestMoveText = !string.IsNullOrEmpty(eval.BestMoveUci) ? eval.BestMoveUci : "None";

            if (eval.MateInMoves.HasValue)
            {
                EngineEvalText = $"M{eval.MateInMoves.Value}";
            }
            else
            {
                int cp = eval.ScoreCentipawns ?? 0;
                bool whiteTurn = Board.Game.IsWhiteTurn;
                int whiteCp = whiteTurn ? cp : -cp;
                double pawns = whiteCp / 100.0;
                EngineEvalText = $"{(pawns >= 0 ? "+" : "")}{pawns:F1}";
            }

            StatusMessage = $"Position evaluated at depth {eval.Depth}.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            StatusMessage = $"Engine evaluation note: {ex.Message}";
        }
    }

    // =========================================================================
    // Feature 1: Explain Last Move
    // =========================================================================

    [RelayCommand]
    public async Task ExplainLastMoveAsync()
    {
        if (Board.Game.MoveHistory.Count == 0)
        {
            StatusMessage = "No moves made yet. Play a move first.";
            return;
        }

        IsThinking = true;
        StatusMessage = "Coach is evaluating your move...";

        try
        {
            var movesUci = Board.Game.MoveHistoryUci;
            var movesSan = Board.Game.MoveHistory.Select(m => m.San).ToList();
            string lastMoveUci = movesUci[^1];
            string lastMoveSan = movesSan[^1];

            // Reconstruct position before last move
            var tempGame = new GeraChessGameAdapter();
            tempGame.LoadFromPosition(Board.Game.StartFen, movesUci.Take(movesUci.Count - 1));
            string fenBefore = tempGame.CurrentFen;
            string fenAfter = Board.CurrentFen;

            // Evaluate position before move
            var evalBefore = await _engineService.EvaluatePositionAsync(fenBefore, depth: 12, timeoutMs: 1200);
            var evalAfter = CurrentEvaluation ?? await _engineService.EvaluatePositionAsync(fenAfter, depth: 12, timeoutMs: 1200);

            int? scoreBefore = evalBefore?.ScoreCentipawns;
            int? scoreAfter = evalAfter?.ScoreCentipawns;

            // Classify move based on centipawn loss from the moving player's perspective
            int cpLoss = 0;
            if (scoreBefore.HasValue && scoreAfter.HasValue)
            {
                // In evalAfter, turn has switched, so invert
                int afterFromMoverPerspective = -scoreAfter.Value;
                cpLoss = Math.Max(0, scoreBefore.Value - afterFromMoverPerspective);
            }

            var classification = cpLoss switch
            {
                <= 15 => CoachMoveClassification.BestMove,
                <= 45 => CoachMoveClassification.Excellent,
                <= 95 => CoachMoveClassification.Good,
                <= 190 => CoachMoveClassification.Inaccuracy,
                <= 320 => CoachMoveClassification.Mistake,
                _ => CoachMoveClassification.Blunder
            };

            string? bestMoveSan = null;
            if (evalBefore != null && !string.IsNullOrEmpty(evalBefore.BestMoveUci))
            {
                var copyGame = new GeraChessGameAdapter(fenBefore);
                var testRes = copyGame.MakeMove(evalBefore.BestMoveUci);
                if (testRes.Success && testRes.Move != null)
                {
                    bestMoveSan = testRes.Move.San;
                }
            }

            var request = new CoachAnalysisRequest(
                FenBefore: fenBefore,
                FenAfter: fenAfter,
                MoveSan: lastMoveSan,
                MoveUci: lastMoveUci,
                BestMoveSan: bestMoveSan,
                BestMoveUci: evalBefore?.BestMoveUci,
                ScoreBeforeCp: scoreBefore,
                ScoreAfterCp: scoreAfter,
                MateIn: evalBefore?.MateInMoves,
                PrincipalVariationSan: evalBefore?.PrincipalVariation.Take(5).ToList() ?? [],
                Classification: classification,
                SkillLevel: SelectedSkillLevel
            );

            var response = await _coachService.AnalyzeMoveAsync(request);
            LastMoveResponse = response;
            HasMoveExplanation = true;

            MoveClassificationBadge = response.Classification switch
            {
                CoachMoveClassification.BestMove => "★ Best Move",
                CoachMoveClassification.Excellent => "✦ Excellent",
                CoachMoveClassification.Good => "✓ Good Move",
                CoachMoveClassification.Inaccuracy => "▲ Inaccuracy",
                CoachMoveClassification.Mistake => "⚠ Mistake",
                CoachMoveClassification.Blunder => "✖ Blunder",
                _ => "Book Move"
            };

            MoveClassificationBrush = response.Classification switch
            {
                CoachMoveClassification.BestMove or CoachMoveClassification.Excellent => "SuccessBrush",
                CoachMoveClassification.Good => "AccentBrush",
                CoachMoveClassification.Inaccuracy => "WarningBrush",
                _ => "ErrorBrush"
            };

            MoveExplanationTitle = $"{response.Classification}: {lastMoveSan}";
            MoveExplanationSummary = response.Summary;
            MoveExplanationTactics = response.TacticalExplanation;
            MoveExplanationStrategy = response.StrategicAdvice;

            MoveScoreBefore = scoreBefore.HasValue ? $"{(scoreBefore.Value / 100.0):+0.0;-0.0;0.0}" : "N/A";
            MoveScoreAfter = scoreAfter.HasValue ? $"{(scoreAfter.Value / 100.0):+0.0;-0.0;0.0}" : "N/A";
            MoveScoreDelta = cpLoss > 0 ? $"-{(cpLoss / 100.0):F2} pawns" : "Optimal (0.00)";
            MoveBestAlternative = bestMoveSan ?? evalBefore?.BestMoveUci ?? "N/A";

            MoveKeyThreats.Clear();
            foreach (var threat in response.KeyThreats)
            {
                MoveKeyThreats.Add(threat);
            }

            StatusMessage = $"Move {lastMoveSan} analyzed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Explanation error: {ex.Message}";
        }
        finally
        {
            IsThinking = false;
        }
    }

    // =========================================================================
    // Feature 2: Threat Radar (Opponent Threats)
    // =========================================================================

    [RelayCommand]
    public async Task ScanOpponentThreatsAsync()
    {
        IsThinking = true;
        StatusMessage = "Scanning opponent threats...";

        try
        {
            var eval = CurrentEvaluation ?? await _engineService.EvaluatePositionAsync(Board.CurrentFen, depth: 12, timeoutMs: 1200);
            var req = new CoachThreatRequest(Board.CurrentFen, eval, SelectedSkillLevel);
            var res = await _coachService.AnalyzeThreatsAsync(req);

            ThreatResponse = res;
            HasThreatAnalysis = true;
            ThreatSummary = res.Summary;
            PrimaryThreatSquare = res.PrimaryThreatSquare;

            DirectThreats.Clear();
            foreach (var t in res.DirectThreats) DirectThreats.Add(t);

            DefensiveIdeas.Clear();
            foreach (var d in res.DefensiveIdeas) DefensiveIdeas.Add(d);

            StatusMessage = $"Threat scan complete ({DirectThreats.Count} threats identified).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Threat scan error: {ex.Message}";
        }
        finally
        {
            IsThinking = false;
        }
    }

    // =========================================================================
    // Feature 3: Progressive Hints (Levels 1 to 4)
    // =========================================================================

    [RelayCommand]
    public async Task RequestNextHintAsync()
    {
        if (HintLevel >= 4)
        {
            StatusMessage = "All 4 progressive hint levels have been revealed.";
            return;
        }

        IsThinking = true;
        int nextLevel = HintLevel + 1;
        StatusMessage = $"Generating Level {nextLevel} hint...";

        try
        {
            var eval = CurrentEvaluation ?? await _engineService.EvaluatePositionAsync(Board.CurrentFen, depth: 12, timeoutMs: 1200);
            var req = new CoachHintRequest(Board.CurrentFen, nextLevel, eval, SelectedSkillLevel);
            var res = await _coachService.GetProgressiveHintAsync(req);

            HintLevel = nextLevel;
            HintLevelBadge = $"Hint {HintLevel} of 4";
            HintTitle = res.HintTitle;
            HintDescription = res.HintText;
            HintTargetSquare = res.TargetSquare;
            CanRequestNextHint = HintLevel < 4;

            StatusMessage = $"Hint Level {HintLevel} revealed.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hint error: {ex.Message}";
        }
        finally
        {
            IsThinking = false;
        }
    }

    [RelayCommand]
    public void ResetHints()
    {
        HintLevel = 0;
        HintLevelBadge = "No Hint Requested";
        HintTitle = "Progressive Hints";
        HintDescription = "Stuck in a position? Request progressive hints from subtle directional nudges to full tactical lines without spoiling the puzzle.";
        HintTargetSquare = null;
        CanRequestNextHint = true;
    }

    // =========================================================================
    // Feature 4: Interactive Q&A
    // =========================================================================

    [RelayCommand]
    public async Task SendQuestionAsync()
    {
        if (string.IsNullOrWhiteSpace(QuestionInput)) return;

        string question = QuestionInput.Trim();
        QuestionInput = string.Empty;

        ChatMessages.Add(new ChatMessageItem
        {
            Sender = "You",
            Text = question,
            IsCoach = false
        });

        IsThinking = true;
        StatusMessage = "AI Coach is answering your question...";

        try
        {
            var eval = CurrentEvaluation ?? await _engineService.EvaluatePositionAsync(Board.CurrentFen, depth: 12, timeoutMs: 1200);
            var req = new CoachQuestionRequest(
                CurrentFen: Board.CurrentFen,
                QuestionText: question,
                Evaluation: eval,
                MoveHistorySan: Board.Game.MoveHistory.Select(m => m.San).ToList(),
                SkillLevel: SelectedSkillLevel
            );

            var res = await _coachService.AnswerQuestionAsync(req);

            string engineEvidence = eval != null
                ? $"Evaluation: {EngineEvalText} | Best move: {BestMoveText} (Depth {eval.Depth})"
                : "Engine: Position verified with rules engine";

            ChatMessages.Add(new ChatMessageItem
            {
                Sender = $"Gemini Coach ({SelectedPersona})",
                Text = res.Answer,
                EngineEvidence = engineEvidence,
                IsLegalMove = res.IsReferencedMoveLegal,
                ValidatedMoveSan = res.ValidatedMoveSan,
                IsCoach = true
            });

            StatusMessage = "Coach response received.";
        }
        catch (Exception ex)
        {
            ChatMessages.Add(new ChatMessageItem
            {
                Sender = "Coach",
                Text = $"I encountered a temporary issue: {ex.Message}. Check your network or offline fallback.",
                IsCoach = true
            });
            StatusMessage = "Response error.";
        }
        finally
        {
            IsThinking = false;
        }
    }

    [RelayCommand]
    public async Task AskQuickQuestionAsync(string questionPrompt)
    {
        QuestionInput = questionPrompt;
        await SendQuestionAsync();
    }

    // =========================================================================
    // Feature 5: Completed Game Review & Turning Points
    // =========================================================================

    [RelayCommand]
    public async Task ReviewCurrentGameAsync()
    {
        if (Board.Game.MoveHistory.Count == 0)
        {
            StatusMessage = "Play some moves first or load a game to review.";
            return;
        }

        IsThinking = true;
        StatusMessage = "Reviewing completed game...";

        try
        {
            var movesUci = Board.Game.MoveHistoryUci.ToList();
            var movesSan = Board.Game.MoveHistory.Select(m => m.San).ToList();

            // Simulate and compute turning points across the match
            var turningPoints = new List<TurningPointItem>();
            var tempGame = new GeraChessGameAdapter(Board.Game.StartFen);

            int whiteTotalLoss = 0;
            int blackTotalLoss = 0;
            int whiteMoveCount = 0;
            int blackMoveCount = 0;

            for (int i = 0; i < movesUci.Count; i++)
            {
                bool isWhite = (i % 2 == 0);
                string fenBefore = tempGame.CurrentFen;
                string moveUci = movesUci[i];
                string moveSan = movesSan[i];

                var evalBefore = await _engineService.EvaluatePositionAsync(fenBefore, depth: 10, timeoutMs: 800);
                tempGame.MakeMove(moveUci);
                string fenAfter = tempGame.CurrentFen;
                var evalAfter = await _engineService.EvaluatePositionAsync(fenAfter, depth: 10, timeoutMs: 800);

                int cpLoss = 0;
                if (evalBefore?.ScoreCentipawns != null && evalAfter?.ScoreCentipawns != null)
                {
                    int afterTurnPerspective = -evalAfter.ScoreCentipawns.Value;
                    cpLoss = Math.Max(0, evalBefore.ScoreCentipawns.Value - afterTurnPerspective);
                }

                if (isWhite)
                {
                    whiteTotalLoss += cpLoss;
                    whiteMoveCount++;
                }
                else
                {
                    blackTotalLoss += cpLoss;
                    blackMoveCount++;
                }

                // Identify turning points: significant score swings >= 100 cp
                if (cpLoss >= 100 || (evalBefore?.MateInMoves != null && evalAfter?.MateInMoves == null))
                {
                    var classification = cpLoss switch
                    {
                        >= 300 => CoachMoveClassification.Blunder,
                        >= 180 => CoachMoveClassification.Mistake,
                        _ => CoachMoveClassification.Inaccuracy
                    };

                    string? bestSan = null;
                    if (evalBefore != null && !string.IsNullOrEmpty(evalBefore.BestMoveUci))
                    {
                        var copyG = new GeraChessGameAdapter(fenBefore);
                        var testRes = copyG.MakeMove(evalBefore.BestMoveUci);
                        if (testRes.Success && testRes.Move != null) bestSan = testRes.Move.San;
                    }

                    turningPoints.Add(new TurningPointItem(
                        MoveNumber: (i / 2) + 1,
                        IsWhiteMove: isWhite,
                        PlayedMoveSan: moveSan,
                        PlayedMoveUci: moveUci,
                        BestMoveSan: bestSan,
                        BestMoveUci: evalBefore?.BestMoveUci,
                        ScoreChangeCp: cpLoss,
                        Classification: classification,
                        FenBefore: fenBefore,
                        FenAfter: fenAfter,
                        Explanation: $"{classification} by {(isWhite ? "White" : "Black")}: lost ~{cpLoss / 100.0:F1} pawns compared to {bestSan ?? "best move"}."
                    ));
                }
            }

            // Accuracy model: Acc = 100 - avg_cp_loss * 0.35, clamped [25, 99]
            int avgWhiteLoss = whiteMoveCount > 0 ? whiteTotalLoss / whiteMoveCount : 0;
            int avgBlackLoss = blackMoveCount > 0 ? blackTotalLoss / blackMoveCount : 0;
            int whiteAcc = Math.Clamp(100 - (int)(avgWhiteLoss * 0.35), 25, 99);
            int blackAcc = Math.Clamp(100 - (int)(avgBlackLoss * 0.35), 25, 99);

            var req = new CoachGameReviewRequest(
                StartFen: Board.Game.StartFen,
                MoveHistorySan: movesSan,
                MoveHistoryUci: movesUci,
                TurningPoints: turningPoints,
                WhiteAccuracyPercentage: whiteAcc,
                BlackAccuracyPercentage: blackAcc,
                SkillLevel: SelectedSkillLevel
            );

            var res = await _coachService.ReviewGameAsync(req);

            WhiteAccuracy = res.WhiteAccuracy;
            BlackAccuracy = res.BlackAccuracy;
            GameReviewSummary = res.OverallSummary;

            TurningPoints.Clear();
            foreach (var tp in res.AnalyzedTurningPoints) TurningPoints.Add(tp);

            KeyTakeaways.Clear();
            foreach (var kt in res.KeyTakeaways) KeyTakeaways.Add(kt);

            // Populate curated lesson & puzzle recommendations based on weaknesses
            await PopulateRecommendationsAsync(res.RecommendedLessonTopics);

            HasGameReview = true;
            StatusMessage = $"Game review finished ({TurningPoints.Count} turning points).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Review error: {ex.Message}";
        }
        finally
        {
            IsThinking = false;
        }
    }

    private async Task PopulateRecommendationsAsync(IReadOnlyList<string> topics)
    {
        RecommendedLessons.Clear();
        RecommendedPuzzles.Clear();

        try
        {
            var lessons = await _lessonService.GetAllLessonsAsync();
            var puzzles = await _puzzleService.GetAllPuzzlesAsync();

            // Match lessons by title or tags
            var matchedLessons = lessons
                .Where(l => topics.Any(t => l.Title.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                                            l.Description.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .Take(3)
                .ToList();

            if (matchedLessons.Count == 0 && lessons.Count > 0)
            {
                matchedLessons = lessons.Take(2).ToList();
            }

            foreach (var l in matchedLessons)
            {
                RecommendedLessons.Add(new LessonRecommendationItem(
                    Id: l.Id,
                    Title: l.Title,
                    Description: l.Description,
                    Stage: l.Stage.ToString()
                ));
            }

            // Match puzzles by difficulty and themes
            var matchedPuzzles = puzzles
                .Where(p => topics.Any(t => p.Theme.ToString().Contains(t, StringComparison.OrdinalIgnoreCase)))
                .Take(3)
                .ToList();

            if (matchedPuzzles.Count == 0 && puzzles.Count > 0)
            {
                matchedPuzzles = puzzles.Take(2).ToList();
            }

            foreach (var p in matchedPuzzles)
            {
                RecommendedPuzzles.Add(new PuzzleRecommendationItem(
                    Id: p.Id,
                    Theme: p.Theme.ToString(),
                    Rating: p.Rating ?? 1500,
                    Description: $"Solve tactical motif: {p.Theme}"
                ));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AiCoach] Recommendation query error: {ex}");
        }
    }

    [RelayCommand]
    public void SelectTurningPoint(TurningPointItem item)
    {
        SelectedTurningPoint = item;
        // Jump board directly to position before blunder so user can study and try moves
        Board.LoadFen(item.FenBefore);
        StatusMessage = $"Loaded Move {item.MoveNumber} ({item.Classification}: {item.PlayedMoveSan}). Try finding {item.BestMoveSan}!";
    }

    [RelayCommand]
    public async Task JumpToLessonAsync(LessonRecommendationItem lesson)
    {
        var mainVm = _serviceProvider.GetService<MainWindowViewModel>();
        if (mainVm != null)
        {
            await mainVm.NavigateToPage<LearnViewModel>();
        }
    }

    [RelayCommand]
    public async Task JumpToPuzzleAsync(PuzzleRecommendationItem puzzle)
    {
        var mainVm = _serviceProvider.GetService<MainWindowViewModel>();
        if (mainVm != null)
        {
            await mainVm.NavigateToPage<PuzzlesViewModel>();
        }
    }

    // =========================================================================
    // General Board Controls
    // =========================================================================

    [RelayCommand]
    public void FlipBoard() => Board.FlipBoard();

    [RelayCommand]
    public async Task UndoMoveAsync()
    {
        Board.UndoMove();
        ResetHints();
        await RunEngineEvaluationAsync();
    }

    [RelayCommand]
    public async Task ResetBoardAsync()
    {
        Board.NewGame();
        ResetHints();
        HasMoveExplanation = false;
        HasThreatAnalysis = false;
        HasGameReview = false;
        await RunEngineEvaluationAsync();
    }
}
