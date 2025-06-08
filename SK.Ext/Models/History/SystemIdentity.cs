namespace SK.Ext.Models.History;

public class SystemIdentity : ISenderIdentity
{
    private SystemIdentity() { }
    public static SystemIdentity Instance { get; } = new SystemIdentity();
}
