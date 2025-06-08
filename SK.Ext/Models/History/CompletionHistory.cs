namespace SK.Ext.Models.History;

public class CompletionHistory
{
    public List<CompletionMessage> Messages { get; init; } = [];

    public CompletionHistory()
    {
    }

    public CompletionHistory(string userMessage)
    {
        ArgumentNullException.ThrowIfNull(userMessage);

        Messages.Add(new CompletionText
        {
            Identity = AgentIdentity.User,
            Content = "Explain the SOLID principles in software development with examples."
        });
    }

    // <summary>
    // Creates a copy of the completion history for a specific agent identity.
    // The messages in the history will be updated to reflect the role of the specified agent.
    // If the agent identity's role is User, the messages will be updated to have the User role for that agent. Other messages will be updated to have the Assistant role.
    // If the agent identity's role is Assistant, the messages will be updated to have the Assistant role for that agent. Other messages will be updated to have the User role.
    // If the agent identity's role is neither User nor Assistant, an exception will be thrown.
    // </summary>
    public CompletionHistory ForAgent(AgentIdentity agentIdentity)
    {
        ArgumentNullException.ThrowIfNull(agentIdentity);

        return new CompletionHistory
        {
            Messages = [.. Messages.Select(m =>
            {
                return m with { Identity = UpdateRole(m.Identity) };
            })]
        };

        AgentIdentity UpdateRole(AgentIdentity identity)
        {
            if (agentIdentity.Role == CompletionRole.User)
            {
                return identity with
                {
                    Role = identity.Name == agentIdentity.Name ? CompletionRole.User : CompletionRole.Assistant
                };
            }
            else if (agentIdentity.Role == CompletionRole.Assistant)
            {
                return identity with
                {
                    Role = identity.Name == agentIdentity.Name ? CompletionRole.Assistant : CompletionRole.User
                };
            }
            else
            {
                throw new InvalidOperationException($"Unknown role: {identity.Role}");
            }
        }
    }
}
