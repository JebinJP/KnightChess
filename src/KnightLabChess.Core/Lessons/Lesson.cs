using System.Text.Json.Serialization;

namespace KnightLabChess.Core.Lessons;

/// <summary>
/// Represents a lesson stage/difficulty level.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LessonStage
{
    Beginner,
    Fundamentals,
    Intermediate,
    Advanced,
    Master
}

/// <summary>
/// Represents a single lesson in the curriculum.
/// </summary>
public class Lesson
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public LessonStage Stage { get; set; }
    public string Category { get; set; } = string.Empty;
    public int Order { get; set; }
    public int Version { get; set; } = 1;
    public List<string> Objectives { get; set; } = [];
    public List<LessonStep> Steps { get; set; } = [];
    public List<string> Prerequisites { get; set; } = [];
}

/// <summary>
/// The type of interaction for a lesson step.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LessonStepType
{
    /// <summary>Explanatory text with an optional board position.</summary>
    Explanation,
    /// <summary>Shows a sequence of moves on the board.</summary>
    Demonstration,
    /// <summary>User must find the correct move(s).</summary>
    Exercise,
    /// <summary>Multiple-choice question.</summary>
    Quiz
}

/// <summary>
/// Represents a single step within a lesson.
/// </summary>
public class LessonStep
{
    public LessonStepType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;

    /// <summary>FEN position to display, if any.</summary>
    public string? Fen { get; set; }

    /// <summary>Moves to demonstrate (SAN notation), for Demonstration steps.</summary>
    public List<string>? DemonstrationMoves { get; set; }

    /// <summary>The correct move(s) in SAN notation, for Exercise steps.</summary>
    public List<string>? CorrectMoves { get; set; }

    /// <summary>Hint text shown after first incorrect attempt.</summary>
    public string? Hint { get; set; }

    /// <summary>Feedback shown after correct answer.</summary>
    public string? CorrectFeedback { get; set; }

    /// <summary>Feedback shown after incorrect answer.</summary>
    public string? IncorrectFeedback { get; set; }

    /// <summary>Quiz options for Quiz steps.</summary>
    public List<QuizOption>? QuizOptions { get; set; }

    /// <summary>Which side the user plays as (for exercise steps).</summary>
    public string? PlayerColor { get; set; }

    /// <summary>Highlighted squares to draw attention to.</summary>
    public List<string>? HighlightSquares { get; set; }

    /// <summary>Arrows to draw on the board [from, to] pairs.</summary>
    public List<string[]>? Arrows { get; set; }
}

/// <summary>
/// Represents a quiz option in a Quiz step.
/// </summary>
public class QuizOption
{
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public string? Explanation { get; set; }
}
