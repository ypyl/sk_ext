namespace SK.Ext.Models.Settings;

public record CompletionSettings
{
    public double Temperature { get; init; } = 0.7;
    public int MaxTokens { get; init; } = 1000;
    public double TopP { get; init; } = 1.0;
    public bool Stream { get; init; } = false;
    public bool Seed { get; init; } = false;
}
