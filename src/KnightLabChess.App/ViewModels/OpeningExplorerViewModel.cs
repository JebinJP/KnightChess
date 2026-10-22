using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnightLabChess.Core.Audio;
using KnightLabChess.Core.Openings;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// ViewModel for the interactive Chess Opening Explorer.
/// Allows studying grandmaster opening repertoires, candidate move trees, and win rates.
/// </summary>
public partial class OpeningExplorerViewModel : PageViewModelBase
{
    private readonly IOpeningService _openingService;
    private readonly IAudioService? _audioService;

    [ObservableProperty]
    private ChessBoardViewModel _board;

    [ObservableProperty]
    private ObservableCollection<OpeningEntry> _openings = [];

    [ObservableProperty]
    private ObservableCollection<OpeningMoveOption> _candidateMoves = [];

    [ObservableProperty]
    private ObservableCollection<string> _moveHistorySan = [];

    [ObservableProperty]
    private OpeningEntry? _currentOpening;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedColorFilter = "All"; // "All", "White", "Black"

    private bool _isExecutingMoveSequence;

    public string[] ColorFilterOptions { get; } = ["All", "White", "Black"];

    public OpeningExplorerViewModel(IOpeningService openingService, IAudioService? audioService = null)
    {
        _openingService = openingService;
        _audioService = audioService;
        _board = new ChessBoardViewModel(audioService);

        PageTitle = "Opening Explorer";
        PageIcon = "📚";

        _board.MoveMade += OnBoardMoveMade;
    }

    public override async Task OnNavigatedToAsync()
    {
        try
        {
            await LoadOpeningsAsync();
            ResetToStartingPosition();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpeningExplorer] OnNavigatedToAsync error: {ex}");
        }
    }

    [RelayCommand]
    public void SelectColorFilter(string? filter)
    {
        if (!string.IsNullOrWhiteSpace(filter))
        {
            SelectedColorFilter = filter;
        }
    }

    private async Task LoadOpeningsAsync()
    {
        var list = await _openingService.GetAllOpeningsAsync();
        ApplyFilter(list);
    }

    private void ApplyFilter(IReadOnlyList<OpeningEntry> source)
    {
        var query = source.AsEnumerable();

        if (SelectedColorFilter == "White")
            query = query.Where(o => o.TargetColor is "White" or "Both");
        else if (SelectedColorFilter == "Black")
            query = query.Where(o => o.TargetColor is "Black" or "Both");

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var q = SearchText.Trim().ToLowerInvariant();
            query = query.Where(o =>
                o.Name.ToLowerInvariant().Contains(q) ||
                o.Variation.ToLowerInvariant().Contains(q) ||
                o.Eco.ToLowerInvariant().Contains(q));
        }

        Openings = new ObservableCollection<OpeningEntry>(query.OrderBy(o => o.Eco));
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = FilterOpeningsAsync();
    }

    partial void OnSelectedColorFilterChanged(string value)
    {
        _ = FilterOpeningsAsync();
    }

    private async Task FilterOpeningsAsync()
    {
        var all = await _openingService.GetAllOpeningsAsync();
        ApplyFilter(all);
    }

    [RelayCommand]
    public async Task SelectOpeningAsync(OpeningEntry? opening)
    {
        if (opening is null) return;

        _isExecutingMoveSequence = true;
        try
        {
            Board.NewGame();
            foreach (var uci in opening.MoveListUci)
            {
                if (!Board.MakeMove(uci))
                {
                    System.Diagnostics.Debug.WriteLine($"[OpeningExplorer] Failed to play opening move: {uci}");
                    break;
                }
            }

            CurrentOpening = opening;
            UpdateMoveHistorySan();
            await UpdateContinuationsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpeningExplorer] SelectOpeningAsync error: {ex}");
        }
        finally
        {
            _isExecutingMoveSequence = false;
        }
    }

    [RelayCommand]
    public void PlayCandidateMove(OpeningMoveOption? option)
    {
        if (option is null) return;
        try
        {
            Board.MakeMove(option.UciMove);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpeningExplorer] PlayCandidateMove error: {ex}");
        }
    }

    [RelayCommand]
    public void ResetPosition()
    {
        _isExecutingMoveSequence = true;
        try
        {
            ResetToStartingPosition();
        }
        finally
        {
            _isExecutingMoveSequence = false;
        }
    }

    [RelayCommand]
    public async Task UndoMoveAsync()
    {
        if (Board.UndoLastMove())
        {
            UpdateMoveHistorySan();
            await UpdateCurrentOpeningAndContinuationsAsync();
        }
    }

    [RelayCommand]
    public void FlipBoard()
    {
        Board.IsFlipped = !Board.IsFlipped;
    }

    private async void OnBoardMoveMade(object? sender, string san)
    {
        if (_isExecutingMoveSequence) return;
        try
        {
            UpdateMoveHistorySan();
            await UpdateCurrentOpeningAndContinuationsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpeningExplorer] OnBoardMoveMade error: {ex}");
        }
    }

    private void ResetToStartingPosition()
    {
        Board.NewGame();
        MoveHistorySan.Clear();
        CurrentOpening = null;
        CandidateMoves.Clear();
        _ = SafeUpdateContinuationsAsync();
    }

    private async Task SafeUpdateContinuationsAsync()
    {
        try
        {
            await UpdateContinuationsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpeningExplorer] UpdateContinuationsAsync error: {ex}");
        }
    }

    private void UpdateMoveHistorySan()
    {
        MoveHistorySan.Clear();
        foreach (var move in Board.Game.MoveHistory)
        {
            MoveHistorySan.Add(move.San);
        }
    }

    private async Task UpdateCurrentOpeningAndContinuationsAsync()
    {
        try
        {
            CurrentOpening = await _openingService.FindOpeningByMovesAsync(Board.Game.MoveHistoryUci);
            await UpdateContinuationsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OpeningExplorer] UpdateCurrentOpeningAndContinuationsAsync error: {ex}");
        }
    }

    private async Task UpdateContinuationsAsync()
    {
        var candidates = await _openingService.GetCandidateMovesAsync(Board.Game.MoveHistoryUci);
        CandidateMoves = new ObservableCollection<OpeningMoveOption>(candidates);
    }
}
