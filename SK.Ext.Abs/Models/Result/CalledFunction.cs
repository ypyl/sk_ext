namespace SK.Ext.Models.Result;

public record CalledFunction
{
    public required string? Id { get; init; }
    public required string FunctionName { get; init; }
    public required string? PluginName { get; init; }
}
