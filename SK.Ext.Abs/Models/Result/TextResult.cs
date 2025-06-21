namespace SK.Ext.Models.Result;

public record TextResult : IContentResult
{
    public required string Text { get; init; }
    public required DateTime? CreatedAt { get; init; }
    public required string? CompletionId { get; init; }
    public required string? SystemFingerprint { get; init; }
    public required string? Model { get; init; }
    public required bool IsStreamed { get; init; }
}
