namespace SK.Ext.Models.History;

public class CompletionImage : ICompletionMessage
{
    public required ISenderIdentity Identity { get; set; }
    public ReadOnlyMemory<byte> Data { get; set; }
    public string? MimeType { get; set; }
}
