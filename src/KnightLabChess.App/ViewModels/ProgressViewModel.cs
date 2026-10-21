using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnightLabChess.Core.Lessons;
using KnightLabChess.Core.Models;
using KnightLabChess.Core.Progress;
using KnightLabChess.Core.Puzzles;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// Represents an achievement or milestone badge in the user journey.
/// </summary>
public class AchievementItem
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public bool IsUnlocked { get; set; }
    public string ProgressText { get; set; } = string.Empty;
}

/// <summary>
/// ViewModel for the user Progress and Statistics dashboard.
/// </summary>
public partial class ProgressViewModel : PageViewModelBase
{
    private readonly IProgressService _progressService;
    private readonly ILessonService _lessonService;
    private readonly IPuzzleService _puzzleService;

    // Metrics
    [ObservableProperty] private int _lessonsCompleted;
    [ObservableProperty] private int _totalLessonsCount = 18;
    [ObservableProperty] private double _lessonCompletionPercent;

    [ObservableProperty] private int _puzzlesSolved;
    [ObservableProperty] private int _totalPuzzlesCount = 8;
    [ObservableProperty] private double _puzzleAccuracy;
    [ObservableProperty] private int _currentStreak;
    [ObservableProperty] private int _longestStreak;

    [ObservableProperty] private int _gamesPlayed;
    [ObservableProperty] private int _gamesWon;
    [ObservableProperty] private int _gamesLost;
    [ObservableProperty] private int _gamesDrawn;
    [ObservableProperty] private double _winRatePercent;

    // Curricula Breakdown
    [ObservableProperty] private int _beginnerCompleted;
    [ObservableProperty] private int _beginnerTotal = 9;
    [ObservableProperty] private int _fundamentalsCompleted;
    [ObservableProperty] private int _fundamentalsTotal = 3;
    [ObservableProperty] private int _intermediateCompleted;
    [ObservableProperty] private int _intermediateTotal = 6;

    // Collections
    [ObservableProperty] private ObservableCollection<AchievementItem> _achievements = [];
    [ObservableProperty] private ObservableCollection<GameRecord> _recentGames = [];

    public ProgressViewModel(
        IProgressService progressService,
        ILessonService lessonService,
        IPuzzleService puzzleService)
    {
        _progressService = progressService;
        _lessonService = lessonService;
        _puzzleService = puzzleService;

        PageTitle = "Progress";
        PageIcon = "📊";
    }

    public override async Task OnNavigatedToAsync()
    {
        await LoadStatisticsAsync();
    }

    [RelayCommand]
    public async Task RefreshStatsAsync()
    {
        await LoadStatisticsAsync();
    }

    private async Task LoadStatisticsAsync()
    {
        var stats = await _progressService.GetUserStatsAsync();
        LessonsCompleted = stats.TotalLessonsCompleted;
        PuzzlesSolved = stats.TotalPuzzlesSolved;
        CurrentStreak = stats.CurrentStreak;
        LongestStreak = Math.Max(stats.LongestStreak, stats.CurrentStreak);
        PuzzleAccuracy = Math.Round(stats.PuzzleAccuracy, 1);

        GamesPlayed = stats.TotalGamesPlayed;
        GamesWon = stats.GamesWon;
        GamesLost = stats.GamesLost;
        GamesDrawn = stats.GamesDrawn;
        WinRatePercent = GamesPlayed > 0
            ? Math.Round((double)GamesWon / GamesPlayed * 100.0, 1)
            : 0.0;

        // Stage breakdown
        var allLessons = await _lessonService.GetAllLessonsAsync();
        TotalLessonsCount = allLessons.Count;
        BeginnerTotal = allLessons.Count(l => l.Stage == LessonStage.Beginner);
        FundamentalsTotal = allLessons.Count(l => l.Stage == LessonStage.Fundamentals);
        IntermediateTotal = allLessons.Count(l => l.Stage == LessonStage.Intermediate);

        var lessonProgress = await _progressService.GetAllLessonProgressAsync();
        var completedSet = lessonProgress.Where(p => p.IsCompleted).Select(p => p.LessonId).ToHashSet();

        BeginnerCompleted = allLessons.Count(l => l.Stage == LessonStage.Beginner && completedSet.Contains(l.Id));
        FundamentalsCompleted = allLessons.Count(l => l.Stage == LessonStage.Fundamentals && completedSet.Contains(l.Id));
        IntermediateCompleted = allLessons.Count(l => l.Stage == LessonStage.Intermediate && completedSet.Contains(l.Id));

        LessonCompletionPercent = TotalLessonsCount > 0
            ? Math.Round((double)LessonsCompleted / TotalLessonsCount * 100.0, 1)
            : 0.0;

        // Recent Games
        var games = await _progressService.GetRecentGamesAsync(8);
        RecentGames = new ObservableCollection<GameRecord>(games);

        // Calculate Achievements
        BuildAchievements();
    }

    private void BuildAchievements()
    {
        var items = new List<AchievementItem>
        {
            new()
            {
                Title = "First Steps",
                Description = "Complete your first lesson in the Academy",
                Icon = "♟",
                IsUnlocked = LessonsCompleted >= 1,
                ProgressText = LessonsCompleted >= 1 ? "Unlocked" : $"{LessonsCompleted}/1"
            },
            new()
            {
                Title = "Scholar of the Board",
                Description = "Complete all Stage 1 Beginner lessons",
                Icon = "🎓",
                IsUnlocked = BeginnerCompleted >= BeginnerTotal && BeginnerTotal > 0,
                ProgressText = $"{BeginnerCompleted}/{BeginnerTotal}"
            },
            new()
            {
                Title = "Tactical Initiate",
                Description = "Solve 5 tactical chess puzzles",
                Icon = "🧩",
                IsUnlocked = PuzzlesSolved >= 5,
                ProgressText = $"{Math.Min(PuzzlesSolved, 5)}/5"
            },
            new()
            {
                Title = "Sharpshooter",
                Description = "Achieve a 5-puzzle solving streak",
                Icon = "🎯",
                IsUnlocked = LongestStreak >= 5,
                ProgressText = $"{Math.Min(LongestStreak, 5)}/5 Streak"
            },
            new()
            {
                Title = "Engine Conqueror",
                Description = "Win a game against the computer engine",
                Icon = "👑",
                IsUnlocked = GamesWon >= 1,
                ProgressText = GamesWon >= 1 ? "Unlocked" : "0/1 Wins"
            },
            new()
            {
                Title = "Grandmaster Mindset",
                Description = "Play 5 complete games in KnightLab Chess",
                Icon = "⚡",
                IsUnlocked = GamesPlayed >= 5,
                ProgressText = $"{Math.Min(GamesPlayed, 5)}/5 Games"
            }
        };

        Achievements = new ObservableCollection<AchievementItem>(items);
    }
}
