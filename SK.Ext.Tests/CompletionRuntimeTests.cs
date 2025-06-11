using FakeItEasy;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SK.Ext.Models;
using SK.Ext.Models.History;
using SK.Ext.Models.Plugin;
using SK.Ext.Models.Result;

namespace SK.Ext.Tests
{
    public class CompletionRuntimeTests
    {
        [Fact]
        public async Task CompletionAgent_ReturnsTextResult_ForSimpleTextCompletion()
        {
            // Arrange
            var fakeService = A.Fake<IChatCompletionService>();
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

            var runtime = new CompletionRuntime(fakeService);

            // Act
            var results = new List<IContentResult>();
            await foreach (var result in runtime.Completion(context, CancellationToken.None))
            {
                results.Add(result);
            }

            // Assert
            Assert.Contains(results, r => r is TextResult text && text.Text == expectedText);
            Assert.Contains(results, r => r is IterationResult);
            Assert.Equal(2, results.Count);
        }

        private class StructuredData
        {
            public string name { get; set; } = string.Empty;
            public int age { get; set; }
        }

        [Fact]
        public async Task CompletionAgent_ReturnsStructuredResult_ForStructuredOutput()
        {
            // Arrange
            var fakeService = A.Fake<IChatCompletionService>();
            var context = new CompletionContextBuilder()
                .WithInitialUserMessage("Give me a JSON object with a name and age.")
                .Build();

            var structuredData = new StructuredData { name = "John", age = 30 };
            var structuredJson = System.Text.Json.JsonSerializer.Serialize(structuredData);
            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, structuredJson);

            A.CallTo(() => fakeService.GetChatMessageContentsAsync(
                    A<ChatHistory>._,
                    A<PromptExecutionSettings>._,
                    A<Kernel>._,
                    A<CancellationToken>._))
                .Returns(Task.FromResult<IReadOnlyList<ChatMessageContent>>(new List<ChatMessageContent> { chatMessageContent }));

            var runtime = new CompletionRuntime(fakeService);

            // Act
            var results = new List<IContentResult>();
            await foreach (var result in runtime.Completion<StructuredData>(context, CancellationToken.None))
            {
                results.Add(result);
            }

            // Assert
            Assert.Contains(results, r => r is TextResult text && text.Text == structuredJson);
            Assert.Contains(results, r => r is StructuredResult<StructuredData> s && s.Result != null && s.Result.name == "John" && s.Result.age == 30);
            Assert.Contains(results, r => r is IterationResult);
            Assert.Equal(3, results.Count);
        }

        [Fact]
        public async Task CompletionAgent_CallsPluginAndReturnsFunctionCall()
        {
            // Arrange
            var fakeService = A.Fake<IChatCompletionService>();
            var plugin = new TestEchoPlugin();
            var plugins = new List<ICompletionPlugin> { plugin };
            var context = new CompletionContextBuilder()
                .WithInitialUserMessage("Echo this: hello!")
                .WithPlugins(plugins)
                .Build();

            // Simulate a function call result from the LLM as a single ChatMessageContent with a FunctionCallContent item
            var functionCallContent = new FunctionCallContent(
                functionName: plugin.Name,
                pluginName: plugin.PluginName,
                id: "1",
                arguments: new KernelArguments { { "input", "hello!" } }
            );

            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, new ChatMessageContentItemCollection { functionCallContent });

            var expectedText = "Hello, user!";
            var chatMessageContent2 = new ChatMessageContent(AuthorRole.Assistant, expectedText);

            // Fake the LLM service to return a single message with the function call
            A.CallTo(() => fakeService.GetChatMessageContentsAsync(
                A<ChatHistory>._,
                A<PromptExecutionSettings>._,
                A<Kernel>._,
                A<CancellationToken>._))
                .ReturnsNextFromSequence(Task.FromResult<IReadOnlyList<ChatMessageContent>>(
                    new List<ChatMessageContent> { chatMessageContent }
                ), Task.FromResult<IReadOnlyList<ChatMessageContent>>(
                    new List<ChatMessageContent> { chatMessageContent2 }
                ));

            var runtime = new CompletionRuntime(fakeService);

            // Act
            var results = new List<IContentResult>();
            await foreach (var result in runtime.Completion(context, CancellationToken.None))
            {
                results.Add(result);
            }

            // Assert
            Assert.Contains(results, r => r is FunctionCall f && f.Name == plugin.Name && f.PluginName == plugin.PluginName);
            Assert.Contains(results, r => r is FunctionExecutionResult f && f.Name == plugin.Name && f.PluginName == plugin.PluginName
                && f.Result?.ToString() == "hello!Runtime information");
        }

        [Fact]
        public async Task CompletionAgent_UsesSeed_WhenEnabledInSettings()
        {
            // Arrange
            var fakeService = A.Fake<IChatCompletionService>();
            var context = new CompletionContextBuilder()
                .WithInitialUserMessage("Hello, assistant!")
                .WithSettings(new Models.Settings.CompletionSettings { Seed = true })
                .Build();

            var expectedText = "Hello, user!";
            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, expectedText);

            A.CallTo(() => fakeService.GetChatMessageContentsAsync(
                    A<ChatHistory>._,
                    A<PromptExecutionSettings>._,
                    A<Kernel>._,
                    A<CancellationToken>._))
                .Returns(Task.FromResult<IReadOnlyList<ChatMessageContent>>(new List<ChatMessageContent> { chatMessageContent }));

            var runtime = new CompletionRuntime(fakeService);

            // Act
            var results = new List<IContentResult>();
            await foreach (var result in runtime.Completion(context, CancellationToken.None))
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
