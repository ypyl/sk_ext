namespace SK.Ext.Models.Result;

public record CallingLLMExceptionResult : IContentResult
{
    public required Exception Exception { get; init; }
    public required bool IsStreamed { get; init; }
}
