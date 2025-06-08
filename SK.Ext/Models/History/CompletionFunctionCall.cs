namespace SK.Ext.Models.History;

public record CompletionFunctionCall : CompletionMessage
{
    public string Name { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public IDictionary<string, object?> Arguments { get; set; } = new Dictionary<string, object?>();
    public object? Result { get; set; } = null;
}
