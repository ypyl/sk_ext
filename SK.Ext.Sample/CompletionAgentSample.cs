using System.ClientModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SK.Ext.Sample;

public class CompletionAgentSample
{
    public static async Task Run(string groqKey)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion("llama-3.3-70b-versatile",
            // Sample groq API key (revoked), replace with your own
            new OpenAI.OpenAIClient(new ApiKeyCredential(groqKey), new OpenAI.OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") }));
        Kernel kernel = builder.Build();

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        var agent = new CompletionAgent(chatCompletionService);
        var context = new CompletionContextBuilder().Build();

        await foreach (var content in agent.Completion(kernel, context, default))
        {
            ProcessingStreamResults(content);
        }
    }

    private static void ProcessingStreamResults(IContentResult content)
    {
        Console.Write(content switch
        {
            TextResult textResult
                => textResult.IsStreamed ? $"{textResult.Text}" : $"[Text Result] {textResult.Text}",

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
