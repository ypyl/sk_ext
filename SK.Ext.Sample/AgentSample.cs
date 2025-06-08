using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OllamaSharp;
using SK.Ext.Models;
using SK.Ext.Models.History;
using SK.Ext.Models.Result;

namespace SK.Ext.Sample;

public class AgentSample
{
    public static async Task Run(string groqKey)
    {
        using var ollamaClient = new OllamaApiClient(
            uriString: "http://localhost:11434",    // E.g. "http://localhost:11434" if Ollama has been started in docker as described above.
            defaultModel: "gemma3:1b" // E.g. "phi3" if phi3 was downloaded as described above.
        );
        // OpenAIChatCompletionService chatCompletionService = new(
        //    modelId: "llama-3.3-70b-versatile",
        //    apiKey: groqKey,
        //    httpClient: new HttpClient { BaseAddress = new Uri("https://api.groq.com/openai/v1") }
        // );

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        IChatCompletionService chatCompletionService = ollamaClient.AsChatCompletionService();
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        var runtime = new CompletionRuntime(chatCompletionService);
        // OpenAIChatCompletionService chatCompletionService = new(
        //     modelId: "llama-3.3-70b-versatile",
        //     apiKey: groqKey,
        //     httpClient: new HttpClient { BaseAddress = new Uri("https://api.groq.com/openai/v1") }
        // );

        // var runtime = new CompletionRuntime(chatCompletionService);
        var context = new CompletionContextBuilder().WithInitialUserMessage("What is the capital of France?").Build();

        await foreach (var content in runtime.Completion(context, default))
        {
            ProcessContentResults(content);
        }
    }

    private static void ProcessContentResults(IContentResult content)
    {
        Console.Write(content switch
        {
            TextResult textResult
                => textResult.IsStreamed ? $"{textResult.Text}" : $"[Text Result] {textResult.Text}\n",

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
}
