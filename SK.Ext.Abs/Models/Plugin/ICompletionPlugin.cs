namespace SK.Ext.Models.Plugin;

public interface ICompletionPlugin
{
    public Delegate FunctionDelegate { get; }
    public string PluginName { get; }
    public string Name { get; }
    public string FunctionDescription { get; }
    public IEnumerable<PluginParameter> Parameters { get; }
    public PluginReturnMetadata Return { get; }
    public bool IsRequired { get; }
}
