namespace SK.Ext.Models.History;

public record ParticipantIdentity
{
    public required string Name { get; init; }
    public required CompletionRole Role { get; init; }

    public static ParticipantIdentity User { get; } = new ParticipantIdentity
    {
        Name = "User",
        Role = CompletionRole.User
    };

    public static ParticipantIdentity Assistant { get; } = new ParticipantIdentity
    {
        Name = "Assistant",
        Role = CompletionRole.Assistant
    };
}
