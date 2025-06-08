namespace SK.Ext.Models.Result;

/// <summary>
/// Return structured output result
/// Only when using <see cref="CompletionRuntime"/>
/// </summary>
/// <typeparam name="T"></typeparam>
public record StructuredResult<T> : IContentResult
{
    public required T? Result { get; init; }
    public required DateTime? CreatedAt { get; init; }
    public required bool IsStreamed { get; init; }
}
