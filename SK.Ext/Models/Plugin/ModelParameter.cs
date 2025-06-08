namespace SK.Ext.Models.Plugin;

public class ModelParameter : PluginParameter
{
    public string Description { get; init; } = string.Empty;
    public object? DefaultValue { get; init; } = null;
    public Type? Type { get; init; } = null;
    public bool IsRequired { get; init; } = false;
}
