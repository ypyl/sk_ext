using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SK.Ext;
using FakeItEasy;

namespace sk.ext.tests;

public class ChatCompletionServiceExtentionsTests
{
    [Fact]
    public async Task StreamChatMessagesWithFunctions_ShouldReturnTextResult()
    {
        // Arrange
        var fakeChatCompletionService = A.Fake<IChatCompletionService>();
        var kernel = new Kernel();
        var chatHistory = new ChatHistory();
        var settings = new PromptExecutionSettings();
        var streamingContent = new StreamingChatMessageContent(AuthorRole.User, "Hello");
        var asyncEnumerable = GetAsyncEnumerable(streamingContent);

        A.CallTo(() => fakeChatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel, default))
            .Returns(asyncEnumerable);

        // Act
        var results = new List<IContentResult>();
        await foreach (var result in fakeChatCompletionService.GetStreamingChatMessageContentsWithFunctions(kernel, chatHistory, settings))
        {
            results.Add(result);
        }

        // Assert
        Assert.Equal(3, results.Count);
        Assert.IsType<TextResult>(results[1]);
        Assert.Equal("Hello", ((TextResult)results[1]).Text);
    }

    [Fact]
    public async Task StreamChatMessagesWithFunctions_ShouldHandleFunctionCall()
    {
        // Arrange
        var fakeChatCompletionService = A.Fake<IChatCompletionService>();
        var kernel = new Kernel();
        kernel.ImportPluginFromFunctions("HelperFunctions",
        [
            kernel.CreateFunctionFromMethod(() => new List<string> { "Squirrel Steals Show", "Dog Wins Lottery" }, "GetLatestNewsTitles", "Retrieves latest news titles."),
        ]);
        var chatHistory = new ChatHistory();
        var settings = new PromptExecutionSettings();
        var streamingContentWithFunc = new StreamingChatMessageContent(AuthorRole.Assistant, "Hello");
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var functionCall = new StreamingFunctionCallUpdateContent
        {
            CallId = "id",
            Name = "GetLatestNewsTitles",
            Arguments = "{}",
            FunctionCallIndex = 0,
            RequestIndex = 0
        };
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        streamingContentWithFunc.Items.Add(functionCall);
        var asyncEnumerableWithFunc = GetAsyncEnumerable(streamingContentWithFunc);

        var streamingContent = new StreamingChatMessageContent(AuthorRole.Assistant, "Hello");
        var asyncEnumerable = GetAsyncEnumerable(streamingContent);

        A.CallTo(() => fakeChatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel, default))
            .ReturnsNextFromSequence(asyncEnumerableWithFunc, asyncEnumerable);

        // Act
        var results = new List<IContentResult>();
        await foreach (var result in fakeChatCompletionService.GetStreamingChatMessageContentsWithFunctions(kernel, chatHistory, settings))
        {
            results.Add(result);
        }

        // Assert
        Assert.Contains(results, r => r is FunctionCall);
    }

    [Fact]
    public async Task StreamChatMessagesWithFunctions_ShouldHandleFunctionException()
    {
        // Arrange
        var fakeChatCompletionService = A.Fake<IChatCompletionService>();
        var kernel = new Kernel();
        kernel.ImportPluginFromFunctions("HelperFunctions",
        [
            kernel.CreateFunctionFromMethod(() => new Exception("Function error"), "GetLatestNewsTitles", "Retrieves latest news titles."),
        ]);
        var chatHistory = new ChatHistory();
        var settings = new PromptExecutionSettings();
        var streamingContentWithFunc = new StreamingChatMessageContent(AuthorRole.User, "Hello");
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var functionCall = new StreamingFunctionCallUpdateContent
        {
            CallId = "id",
            Name = "GetLatestNewsTitles",
            Arguments = "{}",
            FunctionCallIndex = 0,
            RequestIndex = 0
        };
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        streamingContentWithFunc.Items.Add(functionCall);
        var asyncEnumerableWithFunc = GetAsyncEnumerable(streamingContentWithFunc);

        var streamingContent = new StreamingChatMessageContent(AuthorRole.User, "Hello");
        var asyncEnumerable = GetAsyncEnumerable(streamingContent);

        A.CallTo(() => fakeChatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel, default))
            .ReturnsNextFromSequence(asyncEnumerableWithFunc, asyncEnumerable);

        // Act
        var results = new List<IContentResult>();
        await foreach (var result in fakeChatCompletionService.GetStreamingChatMessageContentsWithFunctions(kernel, chatHistory, settings))
        {
            results.Add(result);
        }

        // Assert
        Assert.Contains(results, r => r is FunctionExceptionResult);
    }

    [Fact]
    public async Task GetStreamedChatCompletionResult_ShouldReturnExceptionResult_WhenExceptionOccurs()
    {
        // Arrange
        var chatCompletionService = A.Fake<IChatCompletionService>();
        var chatHistory = new ChatHistory();
        var settings = new PromptExecutionSettings();
        var kernel = new Kernel();
        var token = CancellationToken.None;

        // Return an async enumerable that throws
        var faultyStream = GetFaultyAsyncEnumerable<StreamingChatMessageContent>(
            new StreamingChatMessageContent(AuthorRole.Assistant, "Before Exception"),
            new InvalidOperationException("Simulated streaming failure"));

        A.CallTo(() => chatCompletionService.GetStreamingChatMessageContentsAsync(
                A<ChatHistory>._, A<PromptExecutionSettings>._, A<Kernel>._, A<CancellationToken>._))
            .Returns(faultyStream);

        // Act
        var results = new List<IContentResult>();
        await foreach (var result in chatCompletionService.GetStreamingChatMessageContentsWithFunctions(kernel, chatHistory, settings, token))
        {
            results.Add(result);
        }

        // Assert
        Assert.Equal(4, results.Count);

        var exceptionResult = Assert.IsType<CallingLLMExceptionResult>(results[2]);
        Assert.True(exceptionResult.IsStreamed);
        Assert.IsType<InvalidOperationException>(exceptionResult.Exception);
        Assert.Equal("Simulated streaming failure", exceptionResult.Exception.Message);
    }

    [Fact]
    public async Task StreamChatMessagesWithFunctions_ShouldFallbackToSync_WhenStreamingReturnsEmptyResponse()
    {
        // Arrange
        var fakeChatCompletionService = A.Fake<IChatCompletionService>();
        var kernel = new Kernel();
        var chatHistory = new ChatHistory();
        var settings = new PromptExecutionSettings();

        // Empty streaming response
        var emptyStreamingContent = new StreamingChatMessageContent(AuthorRole.Assistant, "");
        var emptyAsyncEnumerable = GetAsyncEnumerable(emptyStreamingContent);

        // Sync response with actual content
        var syncContent = new ChatMessageContent(AuthorRole.Assistant, "Sync Response");

        A.CallTo(() => fakeChatCompletionService.GetStreamingChatMessageContentsAsync(
                A<ChatHistory>._, A<PromptExecutionSettings>._, A<Kernel>._, A<CancellationToken>._))
            .Returns(emptyAsyncEnumerable);

        A.CallTo(() => fakeChatCompletionService.GetChatMessageContentsAsync(
                A<ChatHistory>._, A<PromptExecutionSettings>._, A<Kernel>._, A<CancellationToken>._))
            .Returns([syncContent]);

        // Act
        var results = new List<IContentResult>();
        await foreach (var result in fakeChatCompletionService.GetStreamingChatMessageContentsWithFunctions(kernel, chatHistory, settings))
        {
            results.Add(result);
        }

        // Assert
        var callingLLMs = results.OfType<CallingLLM>().ToList();
        Assert.Equal(2, callingLLMs.Count);
        Assert.True(callingLLMs[0].IsStreamed);
        Assert.False(callingLLMs[1].IsStreamed);

        var textResults = results.OfType<TextResult>().ToList();
        Assert.Equal(2, textResults.Count);
        Assert.Equal(string.Empty, textResults[0].Text);
        Assert.Equal("Sync Response", textResults[1].Text);

        var iterations = results.OfType<IterationResult>().ToList();
        Assert.Equal(2, iterations.Count);
        Assert.True(iterations[0].IsStreamed);
        Assert.True(iterations[0].IsEmptyResponse);
        Assert.False(iterations[1].IsStreamed);
        Assert.False(iterations[1].IsEmptyResponse);
    }

    [Fact]
    public async Task StreamChatMessagesWithFunctions_ShouldHandleStreamedFunctionResult()
    {
        // Arrange
        var fakeChatCompletionService = A.Fake<IChatCompletionService>();
        var kernel = new Kernel();
        kernel.ImportPluginFromFunctions("HelperFunctions",
        [
            kernel.CreateFunctionFromMethod(() => GetNewsAsync(), "GetLatestNewsTitles", "Retrieves latest news titles."),
        ]);
        var chatHistory = new ChatHistory();
        var settings = new PromptExecutionSettings();
        var streamingContentWithFunc = new StreamingChatMessageContent(AuthorRole.Assistant, "Hello");
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var functionCall = new StreamingFunctionCallUpdateContent
        {
            CallId = "test-id",
            Name = "GetLatestNewsTitles",
            Arguments = "{}",
            FunctionCallIndex = 0,
            RequestIndex = 0
        };
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        streamingContentWithFunc.Items.Add(functionCall);
        var asyncEnumerableWithFunc = GetAsyncEnumerable(streamingContentWithFunc);

        var streamingContent = new StreamingChatMessageContent(AuthorRole.User, "Hello");
        var asyncEnumerable = GetAsyncEnumerable(streamingContent);

        A.CallTo(() => fakeChatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel, default))
            .ReturnsNextFromSequence(asyncEnumerableWithFunc, asyncEnumerable);

        // Act
        var results = new List<IContentResult>();
        await foreach (var result in fakeChatCompletionService.GetStreamingChatMessageContentsWithFunctions(kernel, chatHistory, settings))
        {
            results.Add(result);
        }

        // Assert
        var streamedResults = results.OfType<StreamedFunctionExecutionResult>().ToList();
        var finalResult = results.OfType<FunctionExecutionResult>().Single();

        Assert.Equal(2, streamedResults.Count);
        Assert.Equal("test-id", streamedResults[0].Id);
        Assert.Equal("Squirrel Steals Show", streamedResults[0].Result);
        Assert.Equal("Dog Wins Lottery", streamedResults[1].Result);

        Assert.Equal("test-id", finalResult.Id);
        Assert.IsType<List<object?>>(finalResult.Result);
        var finalList = (List<object?>)finalResult.Result;
        Assert.Equal(2, finalList.Count);
        Assert.Equal("Squirrel Steals Show", finalList[0]);
        Assert.Equal("Dog Wins Lottery", finalList[1]);
    }

    private static async IAsyncEnumerable<string> GetNewsAsync()
    {
        yield return "Squirrel Steals Show";
        await Task.Delay(10); // Simulate network delay
        yield return "Dog Wins Lottery";
    }

    private async IAsyncEnumerable<T> GetFaultyAsyncEnumerable<T>(T beforeException, Exception exception)
    {
        yield return beforeException;
        await Task.Delay(10); // Simulate a little delay
        throw exception;
    }


    private async IAsyncEnumerable<StreamingChatMessageContent> GetAsyncEnumerable(params StreamingChatMessageContent[] contents)
    {
        foreach (var content in contents)
        {
            yield return content;
            await Task.Yield();
        }
    }
}
