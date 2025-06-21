using Microsoft.SemanticKernel.ChatCompletion;
using OllamaSharp;
using SK.Ext.Models;
using SK.Ext.Models.Result;
using SK.Ext.Models.History;
using SK.Ext.Eval;

namespace SK.Ext.Sample;

public class EvaluateRuntimeSample
{
    public static async Task Run(string groqKey)
    {
        using var ollamaClient = new OllamaApiClient(
            uriString: "http://localhost:11434",
            defaultModel: "gemma3:4b"
        );

#pragma warning disable SKEXP0001
        IChatCompletionService chatCompletionService = ollamaClient.AsChatCompletionService();
#pragma warning restore SKEXP0001

        var runtime = new CompletionRuntime(chatCompletionService);
        var context = new CompletionContextBuilder().WithInitialUserMessage("What is the capital of France?").Build();

        // Collect chat messages and response
        var chatMessages = new List<CompletionText>();
        CompletionText? responseText = null;

        await foreach (var content in runtime.Completion(context, default))
        {
            if (content is TextResult textResult)
            {
                var msg = new CompletionText { Content = textResult.Text, Identity = ParticipantIdentity.Assistant };
                responseText = msg;
                Console.Write(textResult.IsStreamed ? $"{textResult.Text}" : $"[Text Result] {textResult.Text}\n");
            }
        }

        if (responseText is null)
        {
            Console.WriteLine("No response to evaluate.");
            return;
        }

        // Evaluate
        var evaluator = new Evaluator(ollamaClient);
        var evalResult = await evaluator.Eval(context.History.OfType<CompletionText>(), responseText);
        Console.WriteLine($"\nEvaluation: Failed={evalResult.Failed}, Rating={evalResult.Rating}, Reason={evalResult.Reason}");
    }
}
