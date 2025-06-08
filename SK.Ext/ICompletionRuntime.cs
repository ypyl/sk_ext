using SK.Ext.Models;
using SK.Ext.Models.Result;

namespace SK.Ext;

public interface ICompletionRuntime
{
    IAsyncEnumerable<IContentResult> Completion(CompletionContext context, CancellationToken token);
    IAsyncEnumerable<IContentResult> Completion<T>(CompletionContext context, CancellationToken token);
}
