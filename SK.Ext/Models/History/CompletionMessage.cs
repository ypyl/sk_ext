namespace SK.Ext.Models.History;

public record CompletionMessage
{
    public required ParticipantIdentity Identity { get; init; }
}
