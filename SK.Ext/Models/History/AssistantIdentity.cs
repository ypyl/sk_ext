namespace SK.Ext.Models.History;

public class AssistantIdentity : ISenderIdentity
{
    public string? Name { get; set; }
    public CompletionRole Role { get; } = CompletionRole.Assistant;
}
