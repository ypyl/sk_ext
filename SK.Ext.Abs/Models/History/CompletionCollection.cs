namespace SK.Ext.Models.History;

public record CompletionCollection : CompletionMessage
{
    public List<CompletionMessage> Messages { get; } = [];
}
