namespace SK.Ext.Models.History;

public record CompletionText : CompletionMessage
{
    public string? Content { get; set; }
}
