namespace SK.Ext.Models.History;

public record CompletionImage : CompletionMessage
{
    public ReadOnlyMemory<byte> Data { get; set; }
    public string? MimeType { get; set; }
}
