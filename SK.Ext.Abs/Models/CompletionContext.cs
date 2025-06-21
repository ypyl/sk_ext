using SK.Ext.Models.History;
using SK.Ext.Models.Plugin;
using SK.Ext.Models.Settings;

namespace SK.Ext.Models;

public record CompletionContext(CompletionSystemMessage SystemMessage, CompletionHistory History, CompletionSettings Settings, IEnumerable<ICompletionPlugin> Plugins)
{
    public CompletionContext SwitchIdentity(ParticipantIdentity identity, string? systemPrompt = null)
    {
        if (string.IsNullOrEmpty(systemPrompt))
        {
            return this with { History = History.ForIdentity(identity) };
        }
        return this with { History = History.ForIdentity(identity), SystemMessage = new CompletionSystemMessage { Prompt = systemPrompt } };
    }

    public CompletionContext AddMessages(IEnumerable<CompletionMessage> messages)
    {
        var updatedHistory = History.AddMessages(messages);
        return this with { History = updatedHistory };
    }
}
