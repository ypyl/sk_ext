namespace SK.Ext.Models.Result;

public record CallingLLM : IContentResult
{
    public required bool IsStreamed { get; init; }
}
