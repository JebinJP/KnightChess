using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnightLabChess.Core.Lessons;
using KnightLabChess.Core.Progress;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// ViewModel for the Home page.
/// </summary>
public partial class HomeViewModel : PageViewModelBase
{
    private readonly ILessonService? _lessonService;
    private readonly IProgressService? _progressService;

    [ObservableProperty]
    private string _welcomeMessage = "Welcome to KnightLab Chess";

    [ObservableProperty]
    private string _subtitle = "Your personal chess academy";

    [ObservableProperty]
    private int _lessonsCompleted;

    [ObservableProperty]
    private int _totalLessons;

    [ObservableProperty]
    private int _puzzlesSolved;

    [ObservableProperty]
    private int _gamesPlayed;

    [ObservableProperty]
    private string _nextLessonTitle = "Loading...";

    [ObservableProperty]
    private string _nextLessonDescription = string.Empty;

    [ObservableProperty]
    private string? _nextLessonId;

    [ObservableProperty]
    private double _learningProgress;

    public HomeViewModel(ILessonService? lessonService = null, IProgressService? progressService = null)
    {
        _lessonService = lessonService;
        _progressService = progressService;
        PageTitle = "Home";
        PageIcon = "🏠";
    }

    public override async Task OnNavigatedToAsync()
    {
        await LoadDashboardDataAsync();
    }

    [RelayCommand]
    private async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        try
        {
            if (_progressService is not null)
            {
                var stats = await _progressService.GetUserStatsAsync();
                LessonsCompleted = stats.TotalLessonsCompleted;
                PuzzlesSolved = stats.TotalPuzzlesSolved;
                GamesPlayed = stats.TotalGamesPlayed;
            }

            if (_lessonService is not null)
            {
                var allLessons = await _lessonService.GetAllLessonsAsync();
                var nextLesson = await _lessonService.GetNextRecommendedLessonAsync();

                TotalLessons = allLessons.Count;
                LearningProgress = TotalLessons > 0
                    ? (double)LessonsCompleted / TotalLessons * 100
                    : 0;

                if (nextLesson is not null)
                {
                    NextLessonTitle = nextLesson.Title;
                    NextLessonDescription = nextLesson.Description;
                    NextLessonId = nextLesson.Id;
                }
            }
            else
            {
                NextLessonTitle = "Play Chess";
                NextLessonDescription = "Start a local human vs human match in the Play arena.";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}
