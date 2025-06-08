namespace SK.Ext.Models.History;

public class SystemIdentity : ISenderIdentity
{
    public CompletionRole Role { get; } = CompletionRole.System;
}
