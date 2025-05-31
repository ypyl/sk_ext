using FakeItEasy;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SK.Ext;

public class CompletionAgentTests
{
    [Fact]
    public async Task CompletionAgent_ReturnsTextResult_ForSimpleTextCompletion()
    {
        // Arrange
        var fakeService = A.Fake<IChatCompletionService>();
        var kernel = new Kernel();
        var context = new CompletionContextBuilder()
            .WithHistory(new CompletionHistory
            {
                Messages = new List<ICompletionMessage>
                {
                    new CompletionText { Role = CompletionRole.User, Content = "Hello, assistant!" }
                }
            })
            .Build();

        var expectedText = "Hello, user!";
        var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, expectedText);

        A.CallTo(() => fakeService.GetChatMessageContentsAsync(
                A<ChatHistory>._,
                A<PromptExecutionSettings>._,
                A<Kernel>._,
                A<CancellationToken>._))
            .Returns(Task.FromResult<IReadOnlyList<ChatMessageContent>>(new List<ChatMessageContent> { chatMessageContent }));

        var agent = new CompletionAgent(fakeService);

        // Act
        var results = new List<IContentResult>();
        await foreach (var result in agent.Completion(kernel, context, CancellationToken.None))
        {
            results.Add(result);
        }

        // Assert
        Assert.Contains(results, r => r is TextResult text && text.Text == expectedText);
        Assert.Contains(results, r => r is IterationResult);
        Assert.Equal(2, results.Count);
    }
}
