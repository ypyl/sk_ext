namespace SK.Ext.Models.Result;

public record FunctionExceptionResult : IContentResult
{
    public required string? Id { get; init; }
    public required Exception Exception { get; init; }
    public required string? FunctionName { get; init; }
    public required string? PluginName { get; init; }
}
