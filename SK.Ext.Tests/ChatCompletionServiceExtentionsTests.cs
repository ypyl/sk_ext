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
        await foreach (var result in fakeChatCompletionService.StreamChatMessagesWithFunctions(kernel, chatHistory, settings))
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
        await foreach (var result in fakeChatCompletionService.StreamChatMessagesWithFunctions(kernel, chatHistory, settings))
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
        await foreach (var result in fakeChatCompletionService.StreamChatMessagesWithFunctions(kernel, chatHistory, settings))
        {
            results.Add(result);
        }

        // Assert
        Assert.Contains(results, r => r is FunctionExceptionResult);
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
