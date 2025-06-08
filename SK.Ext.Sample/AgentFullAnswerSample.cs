using Microsoft.SemanticKernel.Connectors.OpenAI;
using OllamaSharp;
using SK.Ext.Models;
using SK.Ext.Models.History;
using SK.Ext.Models.Result;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SK.Ext.Sample;

public class AgentFullAnswerSample
{
    public static async Task Run(string groqKey)
    {
        using var ollamaClient = new OllamaApiClient(
            uriString: "http://localhost:11434",    // E.g. "http://localhost:11434" if Ollama has been started in docker as described above.
            defaultModel: "gemma3:latest" // E.g. "phi3" if phi3 was downloaded as described above.
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
        var context = new CompletionContextBuilder()
            .WithSystemMessage("You are a helpful assistant that answers questions in detail. Once you have not provided a full answer, append the below:\n==== TO BE CONTINUED ====" +
                         "You will continue the answer in the next message.")
            .WithInitialUserMessage("Write a detailed answer to the question: What is the capital of France? Include historical context, cultural significance, and any notable landmarks.").Build();
        bool isContinued;
        do
        {
            isContinued = false;
            await foreach (var content in runtime.Completion(context, default))
            {
                if (CheckResult(content))
                {
                    isContinued = true;
                    context = context.AddMessages([new CompletionText
                    {
                        Identity = AgentIdentity.Assistant,
                        Content = content is TextResult textResult ? textResult.Text : string.Empty
                    }, new CompletionText
                    {
                        Identity = AgentIdentity.User,
                        Content = "Continue the answer."
                    }]);
                }
            }
        } while (isContinued);
    }

    private static bool CheckResult(IContentResult content)
    {
        if (content is TextResult textResult)
        {
            Console.Write($"[Text Result] {textResult.Text}\n");
            if (textResult.Text.EndsWith("==== TO BE CONTINUED ====", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        if (content is CallingLLMExceptionResult callingLLMExceptionResult)
        {
            Console.Write($"[Calling LLM Exception] {callingLLMExceptionResult.Exception}\n");
            return false;
        }
        if (content is FinishReasonResult finishReasonResult)
        {
            Console.Write($"[Finish Reason] {finishReasonResult.FinishReason}\n");
        }
        return false;
    }
}
