namespace SK.Ext.Models.History;

public record AgentIdentity
{
    public required string Name { get; init; }
    public required CompletionRole Role { get; init; }

    public static AgentIdentity User { get; } = new AgentIdentity
    {
        Name = "User",
        Role = CompletionRole.User
    };

    public static AgentIdentity Assistant { get; } = new AgentIdentity
    {
        Name = "Assistant",
        Role = CompletionRole.Assistant
    };
}
