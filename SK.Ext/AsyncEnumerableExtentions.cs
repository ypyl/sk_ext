using System.Runtime.CompilerServices;
using System.Threading.Channels;

public static class AsyncEnumerableExtentions
{
    public static async IAsyncEnumerable<(int taskId, T item)> MergeWithTaskId<T>(
        this IEnumerable<(int taskId, IAsyncEnumerable<T> stream)> streams,
        int batchSize = 3,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chunks = streams.Chunk(batchSize);
        foreach (var chunk in chunks)
        {
            var chunkChannel = Channel.CreateUnbounded<(int taskId, T item)>();
            var chunkTasks = new List<Task>();

            foreach (var (taskId, stream) in chunk)
            {
                chunkTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var item in stream.WithCancellation(cancellationToken))
                        {
                            await chunkChannel.Writer.WriteAsync((taskId, item), cancellationToken);
                        }
                    }
                    catch (Exception)
                    {
                        // Don't complete the channel writer on exception
                        // Just let the task fail and continue with other tasks
                        throw;
                    }
                }, cancellationToken));
            }

            _ = Task.WhenAll(chunkTasks).ContinueWith(t =>
            {
                // When Task.WhenAll completes, t.Exception will be an AggregateException
                // containing all the exceptions that were thrown by the tasks
                // If any task failed, t.Exception will not be null
                // Propagated exception will be thrown during the read operation
                // on the channel reader, so we can complete the channel writer with it
                chunkChannel.Writer.TryComplete(t.Exception?.Flatten());
            }, cancellationToken);

            await foreach (var item in chunkChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return item;
            }
        }
    }

    public static async IAsyncEnumerable<T> Throttle<T>(this IAsyncEnumerable<T> source,
        IEnumerable<TimeSpan> throttleDurations,
        Func<T, bool> shouldThrottle = null!,
        Func<DateTimeOffset> nowFunc = null!,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var durationEnumerator = throttleDurations.GetEnumerator();
        var lastEmission = DateTimeOffset.MinValue;
        TimeSpan currentThrottle = TimeSpan.Zero;

        bool hasNext = durationEnumerator.MoveNext();
        if (hasNext)
        {
            currentThrottle = durationEnumerator.Current;
        }

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            if (shouldThrottle is null || shouldThrottle(item))
            {
                if (hasNext && durationEnumerator.MoveNext())
                {
                    currentThrottle = durationEnumerator.Current;
                }
                var now = nowFunc?.Invoke() ?? DateTimeOffset.UtcNow;
                if (now - lastEmission >= currentThrottle)
                {
                    lastEmission = now;
                    yield return item;
                }
                // Otherwise skip the item
            }
            else
            {
                // Emit immediately
                yield return item;
            }
        }
    }
}
