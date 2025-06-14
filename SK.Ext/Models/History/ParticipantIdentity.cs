namespace SK.Ext.Models.History;

public record ParticipantIdentity(string Name, CompletionRole Role)
{
    public static ParticipantIdentity User { get; } = new ParticipantIdentity("User", CompletionRole.User);

    public static ParticipantIdentity Assistant { get; } = new ParticipantIdentity("Assistant", CompletionRole.Assistant);
}
