using SK.Ext.Models.History;
using SK.Ext.Models.Plugin;
using SK.Ext.Models.Settings;

namespace SK.Ext.Models;

public class CompletionContextBuilder
{
    private CompletionHistory _history = new() { Messages = [] };
    private CompletionSettings _settings = new();
    private IEnumerable<ICompletionPlugin> _plugins = [];
    private CompletionSystemMessage _systemMessage = new() { Prompt = "You are a helpful assistant." };

    public CompletionContextBuilder WithSystemMessage(string systemPrompt)
    {
        _systemMessage = new CompletionSystemMessage
        {
            Prompt = systemPrompt
        };
        return this;
    }

    public CompletionContextBuilder WithInitialUserMessage(string userMessage)
    {
        _history = new CompletionHistory(userMessage);
        return this;
    }

    public CompletionContextBuilder WithHistoryMessages(IEnumerable<CompletionMessage> messages)
    {
        _history = _history.AddMessages(messages);
        return this;
    }

    public CompletionContextBuilder WithSettings(CompletionSettings settings)
    {
        _settings = settings;
        return this;
    }
    public CompletionContextBuilder WithPlugins(IEnumerable<ICompletionPlugin> plugins)
    {
        _plugins = plugins;
        return this;
    }

    public CompletionContext Build()
    {
        return new CompletionContext(_systemMessage, _history, _settings, _plugins);
    }
}
