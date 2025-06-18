namespace SK.Ext.Models.Result;

/// <summary>
/// Represents a structured result that wraps an exception and its context.
/// </summary>
public record StructuredOutputExceptionResult : IContentResult
{
    /// <summary>
    /// The type of the structured result that failed.
    /// </summary>
    public required string? Type { get; init; }

    /// <summary>
    /// The exception that occurred.
    /// </summary>
    public required Exception Exception { get; init; }

    /// <summary>
    /// The container or original result, if available.
    /// </summary>
    public object? Result { get; init; }
}
