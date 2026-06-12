namespace BazingaGame.Models;

/// <summary>
/// Matches the exact shape ASP.NET Core returns for model validation failures (400).
/// Example: POST /play with player=99 →
/// {
///   "title": "One or more validation errors occurred.",
///   "status": 400,
///   "errors": { "Player": ["The field Player must be between 1 and 5."] }
/// }
/// </summary>
public record ValidationErrorResponse(
    string Title,
    int Status,
    Dictionary<string, string[]> Errors);
