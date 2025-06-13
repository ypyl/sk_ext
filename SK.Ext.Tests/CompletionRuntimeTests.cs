using FakeItEasy;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SK.Ext.Models;
using SK.Ext.Models.Plugin;
using SK.Ext.Models.Result;

namespace SK.Ext.Tests
{
    public class CompletionRuntimeTests
    {
        [Fact]
        public async Task CompletionRuntime_ReturnsTextResult_ForSimpleTextCompletion()
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
        public async Task CompletionRuntime_ReturnsStructuredResult_ForStructuredOutput()
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
        public async Task CompletionRuntime_CallsPluginAndReturnsFunctionCall()
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
        public async Task CompletionRuntime_UsesSeed_WhenEnabledInSettings()
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

        [Fact]
        public async Task CompletionRuntime_CallsPlugin_WhenPluginCallIsRequired()
        {
            // Arrange
            var fakeService = A.Fake<IChatCompletionService>();
            var plugin = new TestEchoPlugin(true); // IsRequired = true
            var plugins = new List<ICompletionPlugin> { plugin };
            var context = new CompletionContextBuilder()
                .WithInitialUserMessage("Please call the echo plugin.")
                .WithPlugins(plugins)
                .Build();

            // Simulate a function call result from the LLM as a single ChatMessageContent with a FunctionCallContent item
            var functionCallContent = new FunctionCallContent(
                functionName: plugin.Name,
                pluginName: plugin.PluginName,
                id: "1",
                arguments: new KernelArguments { { "input", "test input" } }
            );
            var chatMessageContent = new ChatMessageContent(AuthorRole.Assistant, new ChatMessageContentItemCollection { functionCallContent });

            // Simulate a normal message after the function call
            var expectedText = "Echo complete!";
            var chatMessageContent2 = new ChatMessageContent(AuthorRole.Assistant, expectedText);

            // Fake the LLM service to return the function call, then the normal message
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
            Assert.Contains(results, r => r is FunctionExecutionResult f && f.Name == plugin.Name && f.PluginName == plugin.PluginName);
            Assert.Contains(results, r => r is TextResult text && text.Text == expectedText);
        }

        [Fact]
        public async Task CompletionRuntime_UsesSeed_WithDifferentMessageTypes()
        {
            // Arrange
            var textMessage = new SK.Ext.Models.History.CompletionText
            {
                Identity = new SK.Ext.Models.History.ParticipantIdentity { Name = "Assistant", Role = SK.Ext.Models.History.CompletionRole.Assistant },
                Content = "This is a text message."
            };

            // Add a message with image content
            var imageBytes = new byte[] { 1, 2, 3, 4, 5 };
            var imageMessage = new SK.Ext.Models.History.CompletionImage
            {
                Identity = new SK.Ext.Models.History.ParticipantIdentity { Name = "Assistant", Role = SK.Ext.Models.History.CompletionRole.Assistant },
                Data = imageBytes,
                MimeType = "image/png"
            };

            // Add a message with audio content
            var audioBytes = new byte[] { 10, 20, 30 };
            var audioMessage = new SK.Ext.Models.History.CompletionAudio
            {
                Identity = new SK.Ext.Models.History.ParticipantIdentity { Name = "Assistant", Role = SK.Ext.Models.History.CompletionRole.Assistant },
                Data = audioBytes,
                MimeType = "audio/wav"
            };

            var context = new SK.Ext.Models.CompletionContextBuilder()
                .WithInitialUserMessage("Hello!")
                .WithHistoryMessages(new SK.Ext.Models.History.CompletionMessage[] { textMessage, imageMessage, audioMessage })
                .WithSettings(new SK.Ext.Models.Settings.CompletionSettings { Seed = true })
                .Build();

            var fakeService = A.Fake<IChatCompletionService>();

            var runtime = new CompletionRuntime(fakeService);

            // Act
            var results = new List<IContentResult>();
            await foreach (var result in runtime.Completion(context, CancellationToken.None))
            {
                results.Add(result);
            }

            // Assert
            A.CallTo(() => fakeService.GetChatMessageContentsAsync(
                A<ChatHistory>._,
                A<Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings>.That.Matches(settings => settings.Seed.HasValue),
                A<Kernel>._,
                A<CancellationToken>._)).MustHaveHappened();
        }
    }
}
