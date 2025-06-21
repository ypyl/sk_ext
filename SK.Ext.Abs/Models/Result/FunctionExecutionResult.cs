namespace SK.Ext.Models.Result;

public record FunctionExecutionResult : IContentResult
{
    public required string? Id { get; init; }
    public required object? Result { get; init; }
    public required string? Name { get; init; }
    public required string? PluginName { get; init; }
}
