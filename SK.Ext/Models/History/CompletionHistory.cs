namespace SK.Ext.Models.History;

public class CompletionHistory
{
    public required List<CompletionMessage> Messages { get;  init; } = [];

    public CompletionHistory ForAgent(string agentName)
    {
        if (string.IsNullOrWhiteSpace(agentName))
            throw new ArgumentException("Name cannot be null or whitespace.", nameof(agentName));

        return new CompletionHistory
        {
            Messages = [.. Messages.Select(m =>
            {
                if (m.Identity is AgentIdentity agentIdentity)
                {
                    return m with { Identity = UpdateRole(agentIdentity) };
                }
                return m;
            })]
        };

        AgentIdentity UpdateRole(AgentIdentity agentIdentity)
        {
            if (agentIdentity.Role == CompletionRole.User)
            {
                return agentIdentity with
                {
                    Role = agentIdentity.Name == agentName ? CompletionRole.User : CompletionRole.Assistant
                };
            }
            else if (agentIdentity.Role == CompletionRole.Assistant)
            {
                return agentIdentity with
                {
                    Role = agentIdentity.Name == agentName ? CompletionRole.Assistant : CompletionRole.User
                };
            }
            else
            {
                throw new InvalidOperationException($"Unknown role: {agentIdentity.Role}");
            }
        }
    }
}
