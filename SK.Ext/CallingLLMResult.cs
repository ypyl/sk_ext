using SK.Ext.Models.Result;

namespace SK.Ext;

public record CallingLLMResult : IContentResult
{
    public required Microsoft.SemanticKernel.ChatMessageContent Result { get; init; }
}
