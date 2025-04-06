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
            new StreamingChatMessageContent(AuthorRole.User, "Before Exception"),
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
