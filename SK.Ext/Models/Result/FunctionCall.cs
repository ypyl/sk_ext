namespace SK.Ext.Models.Result;

public record FunctionCall : IContentResult
{
    public required string? Id { get; init; }
    public required string Name { get; init; }
    public required IDictionary<string, object?>? Arguments { get; init; }
    public required bool IsStreamed { get; init; }
    public required string? PluginName { get; init; }
}
