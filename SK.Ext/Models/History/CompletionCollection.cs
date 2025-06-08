namespace SK.Ext.Models.History;

public class CompletionCollection : ICompletionMessage
{
    public required ISenderIdentity Identity { get; set; }
    public List<ICompletionMessage> Messages { get; } = [];
}
