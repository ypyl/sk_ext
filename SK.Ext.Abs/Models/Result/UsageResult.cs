namespace SK.Ext.Models.Result;

public record UsageResult : IContentResult
{
    public required int OutputTokenCount { get; init; }
    public required int InputTokenCount { get; init; }
    public required int TotalTokenCount { get; init; }
    public required DateTime? CreatedAt { get; init; }
    public required string? CompletionId { get; init; }
    public required string? SystemFingerprint { get; init; }
    public required string? Model { get; init; }
    public required bool IsStreamed { get; init; }
}
