namespace SK.Ext.Models.History;

public record CompletionText : CompletionMessage
{
    public required string Content { get; init; }
}
