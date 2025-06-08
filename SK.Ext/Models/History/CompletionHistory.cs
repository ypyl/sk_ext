namespace SK.Ext.Models.History;

public class CompletionHistory
{
    private List<ICompletionMessage> _messages = [];
    public required List<ICompletionMessage> Messages
    {
        get => _messages;
        init
        {
            if (value == null || value.Count == 0)
                throw new InvalidOperationException("Messages cannot be null or empty.");
            var systemCount = value.Count(m => m.Identity is SystemIdentity);
            if (systemCount != 1)
                throw new InvalidOperationException($"Messages must contain exactly one message with ISenderIdentity = SystemIdentity, but found {systemCount}.");
            _messages = value;
        }
    }
}
