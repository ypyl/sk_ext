using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SK.Ext.Sample;

public class StreamedFunctionExecutionSample
{
    public static async Task Run(string groqKey, CancellationToken cancellationToken = default)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion("llama-3.3-70b-versatile",
            // Sample groq API key (revoked), replace with your own
            new OpenAI.OpenAIClient(new ApiKeyCredential(groqKey), new OpenAI.OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") }));
        builder.Services.AddLogging(c => c.SetMinimumLevel(LogLevel.Trace));
        Kernel kernel = builder.Build();

        kernel.ImportPluginFromFunctions("HelperFunctions",
        [
            kernel.CreateFunctionFromMethod(() =>
            {
                return GetNewsAsync(cancellationToken);

                static async IAsyncEnumerable<string> GetNewsAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
                {
                    yield return "Squirrel Steals Show";
                    yield return "Dog Wins Lottery";
                    await Task.CompletedTask;
                }
            }, "GetLatestNewsTitles", "Retrieves latest news titles."),
        ]);

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        ChatHistory chatHistory = [];
        chatHistory.AddSystemMessage("You are AI news assistant.");
        chatHistory.AddUserMessage($"What is the latest news?");

        PromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false) };

        await foreach (var content in chatCompletionService.GetStreamingChatMessageContentsWithFunctions(kernel, chatHistory, settings, cancellationToken))
        {
            if (content is StreamedTextResult streamedTextContent)
            {
                Console.WriteLine($"[StreamedText] {streamedTextContent.Text}");
            }
            if (content is StreamedFunctionExecutionResult streamedFunctionExecutionResult)
            {
                Console.WriteLine($"[FunctionCall] {streamedFunctionExecutionResult.Result}");
            }
            if (content is FunctionExecutionResult functionCallContent)
            {
                Console.WriteLine($"[FunctionCallFinal] {functionCallContent.Result}");
            }
            if (content is CallingLLMExceptionResult exceptionResult)
            {
                Console.WriteLine($"[Exception] {exceptionResult.Exception.Message}");
            }
        }
    }
}
