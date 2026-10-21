using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnightLabChess.Core.Openings;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// ViewModel for the interactive Chess Opening Explorer.
/// Allows studying grandmaster opening repertoires, candidate move trees, and win rates.
/// </summary>
public partial class OpeningExplorerViewModel : PageViewModelBase
{
    private readonly IOpeningService _openingService;

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

    public string[] ColorFilterOptions { get; } = ["All", "White", "Black"];

    public OpeningExplorerViewModel(IOpeningService openingService)
    {
        _openingService = openingService;
        _board = new ChessBoardViewModel();

        PageTitle = "Opening Explorer";
        PageIcon = "📚";

        _board.MoveMade += OnBoardMoveMade;
    }

    public override async Task OnNavigatedToAsync()
    {
        await LoadOpeningsAsync();
        ResetToStartingPosition();
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

        CurrentOpening = opening;
        Board.NewGame();
        MoveHistorySan.Clear();

        foreach (var uci in opening.MoveListUci)
        {
            Board.MakeMove(uci);
        }

        UpdateMoveHistorySan();
        await UpdateContinuationsAsync();
    }

    [RelayCommand]
    public async Task PlayCandidateMoveAsync(OpeningMoveOption? option)
    {
        if (option is null) return;

        if (Board.MakeMove(option.UciMove))
        {
            UpdateMoveHistorySan();
            await UpdateCurrentOpeningAndContinuationsAsync();
        }
    }

    [RelayCommand]
    public void ResetPosition()
    {
        ResetToStartingPosition();
    }

    [RelayCommand]
    public async Task UndoMoveAsync()
    {
        Board.UndoLastMove();
        UpdateMoveHistorySan();
        await UpdateCurrentOpeningAndContinuationsAsync();
    }

    [RelayCommand]
    public void FlipBoard()
    {
        Board.IsFlipped = !Board.IsFlipped;
    }

    private async void OnBoardMoveMade(object? sender, string san)
    {
        UpdateMoveHistorySan();
        await UpdateCurrentOpeningAndContinuationsAsync();
    }

    private void ResetToStartingPosition()
    {
        Board.NewGame();
        MoveHistorySan.Clear();
        CurrentOpening = null;
        _ = UpdateContinuationsAsync();
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
        CurrentOpening = await _openingService.FindOpeningByMovesAsync(Board.Game.MoveHistoryUci);
        await UpdateContinuationsAsync();
    }

    private async Task UpdateContinuationsAsync()
    {
        var candidates = await _openingService.GetCandidateMovesAsync(Board.Game.MoveHistoryUci);
        CandidateMoves = new ObservableCollection<OpeningMoveOption>(candidates);
    }
}
