using System.Collections;

namespace SK.Ext.Models.History;

public class CompletionHistory : IEnumerable<CompletionMessage>
{
    private List<CompletionMessage> Messages { get; init; } = [];

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

    /// <summary>
    /// Creates a copy of the completion history for a specific identity.
    /// The messages in the history will be updated to reflect the role of the specified identity.
    /// If the identity's role is User, the messages will be updated to have the User role for that identity. Other messages will be updated to have the Assistant role.
    /// If the identity's role is Assistant, the messages will be updated to have the Assistant role for that identity. Other messages will be updated to have the User role.
    /// If the identity's role is neither User nor Assistant, an exception will be thrown.
    /// </summary>
    public CompletionHistory ForIdentity(ParticipantIdentity targetIdentity)
    {
        ArgumentNullException.ThrowIfNull(targetIdentity);

        if (targetIdentity.Role != CompletionRole.User && targetIdentity.Role != CompletionRole.Assistant)
        {
            throw new InvalidOperationException($"Unknown role: {targetIdentity.Role}");
        }

        CompletionRole selfRole = targetIdentity.Role;
        CompletionRole otherRole = selfRole == CompletionRole.User ? CompletionRole.Assistant : CompletionRole.User;

        return new CompletionHistory
        {
            Messages = [.. Messages.Select(m =>
                m with
                {
                    Identity = m.Identity with
                    {
                        Role = m.Identity.Name == targetIdentity.Name ? selfRole : otherRole
                    }
                }
            )]
        };
    }

    public CompletionHistory AddMessages(IEnumerable<CompletionMessage> messages)
    {
        return new CompletionHistory
        {
            Messages = [.. Messages, .. messages]
        };
    }

    public CompletionMessage this[int index] => Messages[index];
    public int Count => Messages.Count;
    public IEnumerator<CompletionMessage> GetEnumerator() => Messages.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
