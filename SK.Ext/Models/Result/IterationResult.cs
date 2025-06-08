namespace SK.Ext.Models.Result;

public record IterationResult : IContentResult
{
    public required int Iteration { get; init; }
    public required bool IsStreamed { get; init; }
    public required CalledFunction[] CalledFullFunctions { get; init; }
    public required bool IsEmptyResponse { get; init; }
    public required bool IsError { get; init; }
}
