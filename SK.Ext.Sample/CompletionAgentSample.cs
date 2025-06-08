using Microsoft.SemanticKernel.Connectors.OpenAI;
using SK.Ext.Models;
using SK.Ext.Models.History;
using SK.Ext.Models.Result;

namespace SK.Ext.Sample;

public class CompletionAgentSample
{
    public static async Task Run(string groqKey)
    {
        OpenAIChatCompletionService chatCompletionService = new (
            modelId: "llama-3.3-70b-versatile",
            apiKey: groqKey,
            httpClient: new HttpClient { BaseAddress = new Uri("https://api.groq.com/openai/v1") }
        );

        var agent = new CompletionAgent(chatCompletionService);
        var context = new CompletionContextBuilder().WithHistory(new CompletionHistory
        {
            Messages =
            [
                new CompletionText
                {
                    Identity = AgentIdentity.User,
                    Content = "What is the capital of France?"
                }
            ]
        }).Build();

        await foreach (var content in agent.Completion(context, default))
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
