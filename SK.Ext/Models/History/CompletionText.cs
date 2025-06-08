namespace SK.Ext.Models.History;

public class CompletionText : ICompletionMessage
{
    public required ISenderIdentity Identity { get; set; }
    public string? Content { get; set; }
}
