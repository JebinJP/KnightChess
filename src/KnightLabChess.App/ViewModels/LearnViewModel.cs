using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KnightLabChess.Core.Lessons;
using KnightLabChess.Core.Progress;

namespace KnightLabChess.App.ViewModels;

/// <summary>
/// Represents a lesson item in the lesson list.
/// </summary>
public partial class LessonListItem : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private LessonStage _stage;
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private double _masteryScore;
    [ObservableProperty] private int _stepCount;
}

/// <summary>
/// ViewModel for the Learn page — lesson browser and lesson player.
/// </summary>
public partial class LearnViewModel : PageViewModelBase
{
    private readonly ILessonService _lessonService;
    private readonly IProgressService _progressService;

    [ObservableProperty] private ObservableCollection<LessonListItem> _lessons = [];
    [ObservableProperty] private LessonListItem? _selectedLesson;
    [ObservableProperty] private bool _isLessonActive;
    [ObservableProperty] private LessonStage _selectedStage = LessonStage.Beginner;

    // Active lesson state
    [ObservableProperty] private string _currentLessonTitle = string.Empty;
    [ObservableProperty] private int _currentStepIndex;
    [ObservableProperty] private int _totalSteps;
    [ObservableProperty] private string _stepText = string.Empty;
    [ObservableProperty] private string _stepTitle = string.Empty;
    [ObservableProperty] private LessonStepType _currentStepType;
    [ObservableProperty] private ChessBoardViewModel _lessonBoard;
    [ObservableProperty] private bool _showHint;
    [ObservableProperty] private string? _hintText;
    [ObservableProperty] private string? _feedbackText;
    [ObservableProperty] private bool _showFeedback;
    [ObservableProperty] private bool _isFeedbackCorrect;
    [ObservableProperty] private bool _canAdvance;
    [ObservableProperty] private double _progressPercentage;
    [ObservableProperty] private ObservableCollection<QuizOption>? _quizOptions;
    [ObservableProperty] private bool _isExerciseStep;
    [ObservableProperty] private bool _isDemonstrationStep;
    [ObservableProperty] private bool _isDemonstrationPlaying;

    private Lesson? _activeLesson;
    private string? _demoStartingFen;
    private int _correctAnswers;
    private int _totalAttempts;

    public LessonStage[] Stages { get; } = Enum.GetValues<LessonStage>();

    public LearnViewModel(ILessonService lessonService, IProgressService progressService)
    {
        _lessonService = lessonService;
        _progressService = progressService;
        _lessonBoard = new ChessBoardViewModel();
        PageTitle = "Learn";
        PageIcon = "📖";
    }

    public override async Task OnNavigatedToAsync()
    {
        await LoadLessonsAsync();
    }

    [RelayCommand]
    private async Task LoadLessonsAsync()
    {
        IsLoading = true;
        try
        {
            var lessons = await _lessonService.GetLessonsByStageAsync(SelectedStage);
            Lessons.Clear();

            foreach (var lesson in lessons)
            {
                var progress = await _progressService.GetLessonProgressAsync(lesson.Id);
                Lessons.Add(new LessonListItem
                {
                    Id = lesson.Id,
                    Title = lesson.Title,
                    Description = lesson.Description,
                    Stage = lesson.Stage,
                    Category = lesson.Category,
                    IsCompleted = progress?.IsCompleted ?? false,
                    MasteryScore = progress?.MasteryScore ?? 0,
                    StepCount = lesson.Steps.Count
                });
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedStageChanged(LessonStage value)
    {
        _ = LoadLessonsAsync();
    }

    [RelayCommand]
    private void SelectStage(LessonStage stage)
    {
        SelectedStage = stage;
    }

    [RelayCommand]
    private async Task StartLesson(LessonListItem item)
    {
        _activeLesson = await _lessonService.GetLessonByIdAsync(item.Id);
        if (_activeLesson is null) return;

        CurrentLessonTitle = _activeLesson.Title;
        CurrentStepIndex = 0;
        TotalSteps = _activeLesson.Steps.Count;
        _correctAnswers = 0;
        _totalAttempts = 0;
        IsLessonActive = true;

        // Save that we started
        var progress = await _progressService.GetLessonProgressAsync(item.Id)
            ?? new LessonProgress { LessonId = item.Id };
        progress.StartedAt ??= DateTime.UtcNow;
        progress.TotalSteps = TotalSteps;
        progress.LastAccessedAt = DateTime.UtcNow;
        await _progressService.SaveLessonProgressAsync(progress);

        LoadCurrentStep();
    }

    [RelayCommand]
    public async Task StartLessonById(string lessonId)
    {
        var lesson = await _lessonService.GetLessonByIdAsync(lessonId);
        if (lesson is null) return;

        var item = new LessonListItem
        {
            Id = lesson.Id,
            Title = lesson.Title,
            Description = lesson.Description,
            Stage = lesson.Stage,
            Category = lesson.Category,
            StepCount = lesson.Steps.Count
        };

        await StartLesson(item);
    }

    private void LoadCurrentStep()
    {
        if (_activeLesson is null || CurrentStepIndex >= _activeLesson.Steps.Count)
        {
            CompleteLesson();
            return;
        }

        var step = _activeLesson.Steps[CurrentStepIndex];
        StepTitle = step.Title;
        StepText = step.Text;
        CurrentStepType = step.Type;
        ShowHint = false;
        HintText = step.Hint;
        ShowFeedback = false;
        FeedbackText = null;
        CanAdvance = step.Type == LessonStepType.Explanation || step.Type == LessonStepType.Demonstration;
        IsExerciseStep = step.Type == LessonStepType.Exercise;
        QuizOptions = step.QuizOptions is not null ? new ObservableCollection<QuizOption>(step.QuizOptions) : null;
        ProgressPercentage = TotalSteps > 0 ? (double)CurrentStepIndex / TotalSteps * 100 : 0;

        // Load board position
        if (!string.IsNullOrEmpty(step.Fen))
        {
            LessonBoard.LoadFen(step.Fen);
        }

        // Set up exercise constraints
        if (step.Type == LessonStepType.Exercise && step.CorrectMoves is not null)
        {
            LessonBoard.AllowedMoves = step.CorrectMoves;
            LessonBoard.IsInputDisabled = false;

            // Listen for moves
            LessonBoard.MoveMade -= OnExerciseMoveMade;
            LessonBoard.MoveMade += OnExerciseMoveMade;
        }
        else
        {
            LessonBoard.AllowedMoves = null;
            LessonBoard.IsInputDisabled = step.Type != LessonStepType.Demonstration;
        }

        // Set up demonstration
        IsDemonstrationStep = step.Type == LessonStepType.Demonstration && step.DemonstrationMoves is not null && step.DemonstrationMoves.Count > 0;
        _demoStartingFen = step.Fen;
        if (step.Type == LessonStepType.Demonstration)
        {
            LessonBoard.IsInputDisabled = true;
        }
    }

    [RelayCommand]
    private async Task PlayDemonstration()
    {
        if (_activeLesson is null || CurrentStepIndex >= _activeLesson.Steps.Count || IsDemonstrationPlaying) return;
        var step = _activeLesson.Steps[CurrentStepIndex];
        if (step.DemonstrationMoves is null || step.DemonstrationMoves.Count == 0) return;

        IsDemonstrationPlaying = true;
        try
        {
            if (!string.IsNullOrEmpty(_demoStartingFen))
            {
                LessonBoard.LoadFen(_demoStartingFen);
            }

            foreach (var move in step.DemonstrationMoves)
            {
                await Task.Delay(800);
                LessonBoard.MakeMove(move);
            }
        }
        finally
        {
            IsDemonstrationPlaying = false;
        }
    }

    private void OnExerciseMoveMade(object? sender, string san)
    {
        var step = _activeLesson?.Steps[CurrentStepIndex];
        if (step?.CorrectMoves is null) return;

        _totalAttempts++;

        if (step.CorrectMoves.Contains(san))
        {
            _correctAnswers++;
            FeedbackText = step.CorrectFeedback ?? "Correct! Well done.";
            IsFeedbackCorrect = true;
            ShowFeedback = true;
            CanAdvance = true;
            LessonBoard.IsInputDisabled = true;
            LessonBoard.MoveMade -= OnExerciseMoveMade;
        }
        else
        {
            FeedbackText = step.IncorrectFeedback ?? "Not quite. Try again.";
            IsFeedbackCorrect = false;
            ShowFeedback = true;

            // Show hint after first incorrect attempt
            if (!ShowHint && !string.IsNullOrEmpty(HintText))
            {
                ShowHint = true;
            }
        }
    }

    [RelayCommand]
    private void ShowHintAction()
    {
        ShowHint = true;
    }

    [RelayCommand]
    private void AnswerQuiz(QuizOption option)
    {
        _totalAttempts++;

        if (option.IsCorrect)
        {
            _correctAnswers++;
            FeedbackText = option.Explanation ?? "Correct!";
            IsFeedbackCorrect = true;
            CanAdvance = true;
        }
        else
        {
            FeedbackText = option.Explanation ?? "Not quite. Try again.";
            IsFeedbackCorrect = false;
        }
        ShowFeedback = true;
    }

    [RelayCommand]
    private void NextStep()
    {
        CurrentStepIndex++;
        LoadCurrentStep();
    }

    [RelayCommand]
    private void PreviousStep()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
            LoadCurrentStep();
        }
    }

    private async void CompleteLesson()
    {
        if (_activeLesson is null) return;

        double mastery = _totalAttempts > 0
            ? (double)_correctAnswers / _totalAttempts * 100
            : 100;

        var progress = await _progressService.GetLessonProgressAsync(_activeLesson.Id)
            ?? new LessonProgress { LessonId = _activeLesson.Id };

        progress.IsCompleted = true;
        progress.CompletedAt = DateTime.UtcNow;
        progress.LastAccessedAt = DateTime.UtcNow;
        progress.TotalSteps = TotalSteps;
        progress.CurrentStepIndex = TotalSteps;
        progress.CorrectAnswers = _correctAnswers;
        progress.TotalAttempts = _totalAttempts;
        progress.MasteryScore = mastery;

        await _progressService.SaveLessonProgressAsync(progress);

        StepTitle = "Lesson Complete!";
        StepText = $"You completed \"{_activeLesson.Title}\" with a mastery score of {mastery:F0}%.";
        CanAdvance = false;
        IsExerciseStep = false;
        ProgressPercentage = 100;

        // Update the lesson list
        var listItem = Lessons.FirstOrDefault(l => l.Id == _activeLesson.Id);
        if (listItem is not null)
        {
            listItem.IsCompleted = true;
            listItem.MasteryScore = mastery;
        }
    }

    [RelayCommand]
    private void ExitLesson()
    {
        IsLessonActive = false;
        LessonBoard.MoveMade -= OnExerciseMoveMade;
        _activeLesson = null;
    }
}
