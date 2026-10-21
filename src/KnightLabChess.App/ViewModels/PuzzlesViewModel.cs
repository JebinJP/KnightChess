using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnightLabChess.Core.Chess;
using KnightLabChess.Core.Progress;
using KnightLabChess.Core.Puzzles;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// ViewModel for the interactive Tactical Puzzles page.
/// </summary>
public partial class PuzzlesViewModel : PageViewModelBase
{
    private readonly IPuzzleService _puzzleService;
    private readonly IProgressService _progressService;

    [ObservableProperty] private ChessBoardViewModel _board;
    [ObservableProperty] private Puzzle? _currentPuzzle;
    [ObservableProperty] private string _puzzleTitle = "Tactical Puzzle";
    [ObservableProperty] private string _puzzleDescription = string.Empty;
    [ObservableProperty] private PuzzleTheme? _selectedTheme;
    [ObservableProperty] private PuzzleDifficulty? _selectedDifficulty;
    [ObservableProperty] private int _currentMoveIndex;
    [ObservableProperty] private bool _isPuzzleSolved;
    [ObservableProperty] private bool _isIncorrect;
    [ObservableProperty] private bool _showHint;
    [ObservableProperty] private string? _hintText;
    [ObservableProperty] private string? _feedbackText;
    [ObservableProperty] private int _solvedStreak;
    [ObservableProperty] private int _totalPuzzlesSolved;
    [ObservableProperty] private int _rating = 1000;
    [ObservableProperty] private bool _showExplanation;
    [ObservableProperty] private string? _explanationText;

    public PuzzleDifficulty[] Difficulties { get; } = Enum.GetValues<PuzzleDifficulty>();

    public PuzzlesViewModel(IPuzzleService puzzleService, IProgressService progressService)
    {
        _puzzleService = puzzleService;
        _progressService = progressService;
        _board = new ChessBoardViewModel();
        PageTitle = "Puzzles";
        PageIcon = "🧩";

        Board.MoveMade += OnBoardMoveMade;
    }

    public override async Task OnNavigatedToAsync()
    {
        try
        {
            var stats = await _progressService.GetUserStatsAsync();
            TotalPuzzlesSolved = stats.TotalPuzzlesSolved;
            await LoadNextPuzzleAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Puzzles] OnNavigatedToAsync error: {ex}");
        }
    }

    [RelayCommand]
    public async Task LoadNextPuzzleAsync()
    {
        IsLoading = true;
        try
        {
            var puzzle = await _puzzleService.GetNextUnsolvedPuzzleAsync(SelectedDifficulty, SelectedTheme);
            if (puzzle is null)
            {
                var all = await _puzzleService.GetAllPuzzlesAsync();
                puzzle = all.FirstOrDefault();
            }

            if (puzzle is not null)
            {
                LoadPuzzle(puzzle);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void LoadPuzzle(Puzzle puzzle)
    {
        CurrentPuzzle = puzzle;
        PuzzleTitle = puzzle.Title;
        PuzzleDescription = puzzle.Description;
        HintText = puzzle.Hint;
        ExplanationText = puzzle.Explanation;
        Rating = puzzle.Rating ?? 1000;
        CurrentMoveIndex = 0;
        IsPuzzleSolved = false;
        IsIncorrect = false;
        ShowHint = false;
        ShowExplanation = false;
        FeedbackText = null;

        Board.LoadFen(puzzle.Fen);
        Board.IsFlipped = string.Equals(puzzle.PlayerColor, "Black", StringComparison.OrdinalIgnoreCase);
        Board.IsInputDisabled = false;
    }

    private async void OnBoardMoveMade(object? sender, string san)
    {
        try
        {
            if (CurrentPuzzle is null || IsPuzzleSolved) return;

            // The move played on board is the last in history
            var playedUci = Board.Game.MoveHistoryUci.LastOrDefault();
            if (string.IsNullOrEmpty(playedUci)) return;

            if (CurrentMoveIndex < CurrentPuzzle.SolutionMoves.Count)
            {
                var expectedUci = CurrentPuzzle.SolutionMoves[CurrentMoveIndex];

                // Normalize promotion characters (e.g., e7e8q)
                if (string.Equals(playedUci, expectedUci, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentMoveIndex++;
                    IsIncorrect = false;

                    // Did the user complete the full solution?
                    if (CurrentMoveIndex >= CurrentPuzzle.SolutionMoves.Count)
                    {
                        await HandlePuzzleSolvedAsync();
                    }
                    else
                    {
                        // Opponent auto-reply move
                        FeedbackText = "Great move! Keep going...";
                        Board.IsInputDisabled = true;

                        await Task.Delay(500);

                        if (CurrentMoveIndex < CurrentPuzzle.SolutionMoves.Count)
                        {
                            var replyUci = CurrentPuzzle.SolutionMoves[CurrentMoveIndex];
                            Board.MakeMove(replyUci);
                            CurrentMoveIndex++;
                        }

                        Board.IsInputDisabled = false;
                    }
                }
                else
                {
                    // Incorrect move
                    IsIncorrect = true;
                    FeedbackText = "Not the best move. Try again!";
                    SolvedStreak = 0;

                    await Task.Delay(600);

                    // Undo player's incorrect move
                    Board.UndoMove();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Puzzles] OnBoardMoveMade error: {ex}");
        }
    }

    private async Task HandlePuzzleSolvedAsync()
    {
        try
        {
            IsPuzzleSolved = true;
            ShowExplanation = true;
            FeedbackText = "✓ Puzzle Solved! Brilliant calculation.";
            SolvedStreak++;
            TotalPuzzlesSolved++;
            Board.IsInputDisabled = true;

            if (CurrentPuzzle is not null)
            {
                var progress = new PuzzleProgress
                {
                    PuzzleId = CurrentPuzzle.Id,
                    IsSolved = true,
                    SolvedAt = DateTime.UtcNow,
                    LastAttemptAt = DateTime.UtcNow,
                    Attempts = 1
                };
                await _progressService.SavePuzzleProgressAsync(progress);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Puzzles] HandlePuzzleSolvedAsync error: {ex}");
        }
    }

    [RelayCommand]
    private void ShowHintAction()
    {
        ShowHint = true;
    }

    [RelayCommand]
    private async Task RetryPuzzleAsync()
    {
        if (CurrentPuzzle is not null)
        {
            LoadPuzzle(CurrentPuzzle);
        }
    }

    partial void OnSelectedDifficultyChanged(PuzzleDifficulty? value)
    {
        _ = LoadNextPuzzleAsync();
    }
}
