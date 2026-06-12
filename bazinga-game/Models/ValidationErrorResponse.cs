namespace BazingaGame.Models;

/// <summary>
/// Documents the exact shape ASP.NET Core returns for model validation failures (400).
/// The schema is provided manually in SwaggerGen (Program.cs MapType) because
/// Swashbuckle cannot introspect Dictionary&lt;string, string[]&gt; record properties.
/// Example response:
/// {
///   "title": "One or more validation errors occurred.",
///   "status": 400,
///   "errors": { "Player": ["The field Player must be between 1 and 5."] }
/// }
/// </summary>
public class ValidationErrorResponse;
