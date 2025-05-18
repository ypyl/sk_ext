using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SK.Ext.Sample;

public class RemoveDuplicatedFunctionCallResultsSample
{
    public static async Task Run(string groqKey)
    {
        await foreach (var content in SetUpAssistantKernel(groqKey))
        {
            ProcessingStreamResults(content);
        }
    }

    private static void ProcessingStreamResults(IContentResult content)
    {
        Console.Write(content switch
        {
            TextResult textResult
                => $"[Text Result] {textResult.Text}",

            StreamedTextResult streamedTextResult
                => $"{streamedTextResult.Text}",

            FinishReasonResult finishReasonResult
                => $"[Finish Reason] {finishReasonResult.FinishReason}\n",

            CallingLLM callingLLM
                => $"[Calling LLM] Streamed: {callingLLM.IsStreamed}\n",

            FunctionCall functionCall
                => $"[Function Call] {functionCall.Name}\n" +
                   string.Join("\n", (functionCall.Arguments ?? new Dictionary<string, object?>()).Select(arg => $"{arg.Key}: {arg.Value}")) +
                   (functionCall.Arguments?.Any() == true ? "\n" : string.Empty),

            FunctionExceptionResult functionExceptionResult
                => $"[Exception] {functionExceptionResult.Exception.Message}\n",

            FunctionExecutionResult functionResult
                => $"[Result] {functionResult.Result?.ToString() ?? string.Empty}\n",

            UsageResult usageResult
                => $"[Usage] Input Tokens: {usageResult.InputTokenCount}, Output Tokens: {usageResult.OutputTokenCount}, Total Tokens: {usageResult.TotalTokenCount}\n",

            IterationResult iterationResult
                => $"[Iteration] Iteration: {iterationResult.Iteration}, Is Streamed: {iterationResult.IsStreamed}, Function Calls: {string.Join(", ", iterationResult.CalledFullFunctions.Select(fc => fc.FunctionName))}\n",

            CallingLLMExceptionResult callingLLMExceptionResult
                => $"[Calling LLM Exception] {callingLLMExceptionResult.Exception}\n",
            _ => string.Empty
        });
    }

    private static async IAsyncEnumerable<IContentResult> SetUpAssistantKernel(string groqKey, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion("llama-3.3-70b-versatile",
            // Sample groq API key (revoked), replace with your own
            new OpenAI.OpenAIClient(new ApiKeyCredential(groqKey), new OpenAI.OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") }));
        builder.Services.AddLogging(c => c.SetMinimumLevel(LogLevel.Trace));
        Kernel kernel = builder.Build();

        kernel.ImportPluginFromFunctions("HelperFunctions",
        [
            kernel.CreateFunctionFromMethod(
                () => new List<string> { "Prices is up!", "Fuel price is down!" },
                "GetLatesNews",
                "Retrieves latest news.",
                [
                    new KernelParameterMetadata("topic"),
                ]),
        ]);

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        ChatHistory chatHistory = [];
        chatHistory.AddSystemMessage("You are Info assistant. Always use the following topics to answer: news, weather, sport, and finance.");
        chatHistory.AddUserMessage($"What is going on in the world?");

        PromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false) };

        await foreach (var content in chatCompletionService.GetStreamingChatMessageContentsWithFunctions(kernel, chatHistory, settings, cancellationToken))
        {
            yield return content;
            if (content is IterationResult iterationResult)
            {
                foreach (var mergedIds in chatHistory.RemoveDuplicatedFunctionParallelCallResults())
                {
                    foreach (var id in mergedIds)
                    {
                        var functionCall = iterationResult.CalledFullFunctions.FirstOrDefault(x => x.Id == id);
                        if (functionCall != null)
                        {
                            Console.WriteLine($"Function call {functionCall.FunctionName} with ID {id} has been merged.");
                        }
                    }
                }
            }
        }
    }
}
