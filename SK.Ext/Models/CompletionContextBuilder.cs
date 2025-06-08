using SK.Ext.Models.History;
using SK.Ext.Models.Plugin;
using SK.Ext.Models.Settings;

namespace SK.Ext.Models;

public class CompletionContextBuilder
{
    private CompletionHistory _history = new()
    {
        Messages = [new CompletionText { Identity = new SystemIdentity(), Content = "You are a helpful assistant." }]
    };
    private CompletionSettings _settings = new();
    private IEnumerable<ICompletionPlugin> _plugins = [];

    public CompletionContextBuilder WithHistory(CompletionHistory history)
    {
        _history = history;
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
        return new CompletionContext(_history, _settings, _plugins);
    }
}
