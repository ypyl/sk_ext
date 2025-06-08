namespace SK.Ext.Models.Result;

public record StreamedFunctionExecutionResult : IContentResult
{
    public required string? Id { get; init; }
    public required object? Result { get; init; }
    public required string? FunctionName { get; init; }
    public required string? PluginName { get; init; }
}
