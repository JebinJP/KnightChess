using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Engine;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// ViewModel for the interactive Stockfish Analysis board with live Evaluation Bar.
/// </summary>
public partial class AnalysisViewModel : PageViewModelBase
{
    private readonly IEngineService _engineService;
    private CancellationTokenSource? _evalCts;

    [ObservableProperty] private ChessBoardViewModel _board;
    [ObservableProperty] private bool _isAnalyzing = true;
    [ObservableProperty] private string _evaluationScoreText = "+0.0";
    [ObservableProperty] private double _whiteAdvantagePercentage = 50.0;
    [ObservableProperty] private int _evalDepth = 12;
    [ObservableProperty] private long _nodesPerSecond;
    [ObservableProperty] private string? _bestMoveUci;
    [ObservableProperty] private string _principalVariationText = string.Empty;
    [ObservableProperty] private string _customFenInput = string.Empty;
    [ObservableProperty] private string _engineStatusText = "Stockfish Active";
    [ObservableProperty] private bool _isWhiteAdvantage = true;

    public AnalysisViewModel(IEngineService engineService)
    {
        _engineService = engineService;
        _board = new ChessBoardViewModel();
        PageTitle = "Analysis";
        PageIcon = "🔍";

        Board.MoveMade += async (_, _) =>
        {
            CustomFenInput = Board.CurrentFen;
            if (IsAnalyzing)
            {
                await RunEvaluationAsync();
            }
        };
    }

    public override async Task OnNavigatedToAsync()
    {
        await _engineService.InitializeAsync();
        EngineStatusText = $"{_engineService.EngineName} Ready";
        CustomFenInput = Board.CurrentFen;

        if (IsAnalyzing)
        {
            await RunEvaluationAsync();
        }
    }

    [RelayCommand]
    public async Task RunEvaluationAsync()
    {
        _evalCts?.Cancel();
        _evalCts = new CancellationTokenSource();
        var ct = _evalCts.Token;

        try
        {
            EngineStatusText = "Analyzing position...";
            var eval = await _engineService.EvaluatePositionAsync(Board.CurrentFen, depth: 14, timeoutMs: 1200, cancellationToken: ct);
            if (eval is null || ct.IsCancellationRequested) return;

            EvalDepth = eval.Depth;
            NodesPerSecond = eval.NodesPerSecond;
            BestMoveUci = eval.BestMoveUci;
            PrincipalVariationText = string.Join(" ", eval.PrincipalVariation.Take(6));

            // Format score from White's perspective
            int? cp = eval.ScoreCentipawns;
            int? mate = eval.MateInMoves;

            // In UCI, scores are relative to active player to move
            bool whiteToMove = Board.Game.IsWhiteTurn;
            int whiteScoreCp = cp.HasValue ? (whiteToMove ? cp.Value : -cp.Value) : 0;
            int? whiteMateIn = mate.HasValue ? (whiteToMove ? mate.Value : -mate.Value) : null;

            if (whiteMateIn.HasValue)
            {
                EvaluationScoreText = $"M{whiteMateIn.Value}";
                WhiteAdvantagePercentage = whiteMateIn.Value > 0 ? 100.0 : 0.0;
                IsWhiteAdvantage = whiteMateIn.Value > 0;
            }
            else
            {
                double pawns = whiteScoreCp / 100.0;
                EvaluationScoreText = $"{(pawns >= 0 ? "+" : "")}{pawns:F1}";
                IsWhiteAdvantage = whiteScoreCp >= 0;

                // Smooth winning chance sigmoid: 50 + 50 * (2 / (1 + exp(-0.004 * cp)) - 1)
                double winChance = 50.0 + (2.0 / (1.0 + Math.Exp(-0.0035 * whiteScoreCp)) - 1.0) * 50.0;
                WhiteAdvantagePercentage = Math.Clamp(winChance, 3.0, 97.0);
            }

            EngineStatusText = $"{_engineService.EngineName} (Depth {EvalDepth})";
        }
        catch (OperationCanceledException)
        {
            // Expected on fast moves
        }
        catch (Exception ex)
        {
            EngineStatusText = $"Evaluation error: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ToggleAnalysisAsync()
    {
        IsAnalyzing = !IsAnalyzing;
        if (IsAnalyzing)
        {
            await RunEvaluationAsync();
        }
        else
        {
            await _engineService.StopAsync();
            EngineStatusText = "Analysis Paused";
        }
    }

    [RelayCommand]
    private async Task LoadCustomFenAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomFenInput)) return;

        try
        {
            Board.LoadFen(CustomFenInput.Trim());
            if (IsAnalyzing)
            {
                await RunEvaluationAsync();
            }
        }
        catch (Exception ex)
        {
            EngineStatusText = $"Invalid FEN: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task ResetToStartingPositionAsync()
    {
        Board.NewGame();
        CustomFenInput = Board.CurrentFen;
        if (IsAnalyzing)
        {
            await RunEvaluationAsync();
        }
    }

    [RelayCommand]
    private void FlipBoard()
    {
        Board.FlipBoard();
    }

    [RelayCommand]
    private async Task UndoMoveAsync()
    {
        Board.UndoMove();
        CustomFenInput = Board.CurrentFen;
        if (IsAnalyzing)
        {
            await RunEvaluationAsync();
        }
    }

    [RelayCommand]
    private void PlayBestMove()
    {
        if (!string.IsNullOrEmpty(BestMoveUci))
        {
            Board.MakeMove(BestMoveUci);
        }
    }
}
