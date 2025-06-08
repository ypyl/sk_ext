namespace SK.Ext.Models.History;

public class UserIdentity : ISenderIdentity
{
    public string? Name { get; set; }
    public CompletionRole Role { get; } = CompletionRole.User;
}
