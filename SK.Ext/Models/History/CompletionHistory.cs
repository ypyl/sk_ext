namespace SK.Ext.Models.History;

public class CompletionHistory
{
    public IEnumerable<CompletionMessage> Messages { get; init; } = [];

    public CompletionHistory()
    {
    }

    public CompletionHistory(string userMessage)
    {
        ArgumentNullException.ThrowIfNull(userMessage);

        Messages = [new CompletionText
        {
            Identity = ParticipantIdentity.User,
            Content = userMessage
        }];
    }

    // <summary>
    // Creates a copy of the completion history for a specific identity.
    // The messages in the history will be updated to reflect the role of the specified identity.
    // If the identity's role is User, the messages will be updated to have the User role for that identity. Other messages will be updated to have the Assistant role.
    // If the identity's role is Assistant, the messages will be updated to have the Assistant role for that identity. Other messages will be updated to have the User role.
    // If the identity's role is neither User nor Assistant, an exception will be thrown.
    // </summary>
    public CompletionHistory ForIdentity(ParticipantIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return new CompletionHistory
        {
            Messages = [.. Messages.Select(m =>
            {
                return m with { Identity = UpdateRole(m.Identity) };
            })]
        };

        ParticipantIdentity UpdateRole(ParticipantIdentity identity)
        {
            if (identity.Role == CompletionRole.User)
            {
                return identity with
                {
                    Role = identity.Name == identity.Name ? CompletionRole.User : CompletionRole.Assistant
                };
            }
            else if (identity.Role == CompletionRole.Assistant)
            {
                return identity with
                {
                    Role = identity.Name == identity.Name ? CompletionRole.Assistant : CompletionRole.User
                };
            }
            else
            {
                throw new InvalidOperationException($"Unknown role: {identity.Role}");
            }
        }
    }

    public CompletionHistory AddMessages(IEnumerable<CompletionMessage> messages)
    {
        return new CompletionHistory
        {
            Messages = [.. Messages, .. messages]
        };
    }
}
