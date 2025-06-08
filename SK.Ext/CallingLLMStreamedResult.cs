using Microsoft.SemanticKernel;
using SK.Ext.Models.Result;

namespace SK.Ext;

public record CallingLLMStreamedResult : IContentResult
{
    public required StreamingChatMessageContent Result { get; init; }
}
