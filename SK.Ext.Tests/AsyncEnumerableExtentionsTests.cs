using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class AsyncEnumerableExtentionsTests
{
    [Fact]
    public async Task MergeWithTaskId_ShouldMergeStreams()
    {
        // Arrange
        var stream1 = AsyncEnumerable();
        var stream2 = AsyncEnumerable(100);
        var stream3 = AsyncEnumerable(200);
        var streams = new List<(int taskId, IAsyncEnumerable<int> stream)>
        {
            (1, stream1),
            (2, stream2),
            (3, stream3)
        };

        // Act
        var result = await streams.MergeWithTaskId().ToListAsync();

        // Assert
        Assert.Equal(15, result.Count);
        foreach (var item in result)
        {
            int taskId = item.taskId;
            int value = item.item;

            if (taskId == 1)
                Assert.InRange(value, 0, 4);
            else if (taskId == 2)
                Assert.InRange(value, 100, 104);
            else if (taskId == 3)
                Assert.InRange(value, 200, 204);
            else
                Assert.Fail($"Unexpected taskId: {taskId}");
        }
    }

    [Fact]
    public async Task MergeWithTaskId_ShouldHandleEmptyStreams()
    {
        // Arrange
        var stream1 = AsyncEnumerable();
        var emptyStream = EmptyAsyncEnumerable();
        var streams = new List<(int taskId, IAsyncEnumerable<int> stream)>
        {
            (1, stream1),
            (2, emptyStream)
        };

        // Act
        var result = await streams.MergeWithTaskId().ToListAsync();

        // Assert
        Assert.Equal(5, result.Count);
        foreach (var item in result)
        {
            int taskId = item.taskId;
            int value = item.item;

            Assert.Equal(1, taskId);
            Assert.InRange(value, 0, 4);
        }
    }

    [Fact]
    public async Task MergeWithTaskId_ShouldHandleChunking()
    {
        // Arrange
        var streams = new List<(int taskId, IAsyncEnumerable<int> stream)>();
        for (int i = 1; i <= 5; i++)
        {
            streams.Add((i, AsyncEnumerable((i - 1) * 100)));
        }

        // Act
        var result = await streams.MergeWithTaskId(batchSize: 2).ToListAsync();

        // Assert
        Assert.Equal(25, result.Count);
        // Verify that all items from all streams were included
    }

    [Fact]
    public async Task Throttle_ShouldLimitEmissionRate()
    {
        // Arrange
        var source = AsyncEnumerable(0, 0);
        var throttleDurations = new[] { TimeSpan.FromMilliseconds(50) };

        // Act
        var result = new List<int>();
        await foreach (var item in source.Throttle(throttleDurations, (i) => true, () => DateTimeOffset.UtcNow))
        {
            result.Add(item);
        }

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task Throttle_ShouldNotThrottleItemsNotMatchingPredicate()
    {
        // Arrange
        var source = AsyncEnumerable(0, 0);
        var throttleDurations = new[] { TimeSpan.FromMilliseconds(100) };

        // Act
        var result = new List<int>();
        await foreach (var item in source.Throttle(
            throttleDurations,
            shouldThrottle: i => i % 2 == 0)) // Only throttle even numbers
        {
            result.Add(item);
        }

        // Assert
        Assert.Equal(3, result.Count);
    }

    private async IAsyncEnumerable<int> AsyncEnumerable(int start = 0, int delay = 10)
    {
        for (int i = 0; i < 5; i++)
        {
            if (delay > 0)
                await Task.Delay(delay);
            yield return start + i;
        }
    }

    private async IAsyncEnumerable<int> EmptyAsyncEnumerable()
    {
        await Task.CompletedTask; // To make the method truly async
        yield break;
    }
}
