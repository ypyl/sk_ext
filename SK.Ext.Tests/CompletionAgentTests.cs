using FakeItEasy;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SK.Ext.Models;
using SK.Ext.Models.History;
using SK.Ext.Models.Result;

namespace SK.Ext.Tests
{
    public class CompletionAgentTests
    {
        [Fact]
        public async Task CompletionAgent_ReturnsTextResult_ForSimpleTextCompletion()
        {
            // Arrange
            var fakeService = A.Fake<IChatCompletionService>();
            var kernel = new Kernel();
            var context = new CompletionContextBuilder()
                .WithInitialUserMessage("Hello, assistant!")
                .Build();

            var expectedText = "Hello, user!";
            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, expectedText);

            A.CallTo(() => fakeService.GetChatMessageContentsAsync(
                    A<ChatHistory>._,
                    A<PromptExecutionSettings>._,
                    A<Kernel>._,
                    A<CancellationToken>._))
                .Returns(Task.FromResult<IReadOnlyList<ChatMessageContent>>(new List<ChatMessageContent> { chatMessageContent }));

            var agent = new CompletionRuntime(fakeService);

            // Act
            var results = new List<IContentResult>();
            await foreach (var result in agent.Completion(context, CancellationToken.None))
            {
                results.Add(result);
            }

            // Assert
            Assert.Contains(results, r => r is TextResult text && text.Text == expectedText);
            Assert.Contains(results, r => r is IterationResult);
            Assert.Equal(2, results.Count);
        }
    }
}
