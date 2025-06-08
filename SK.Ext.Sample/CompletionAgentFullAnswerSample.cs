using Microsoft.SemanticKernel.Connectors.OpenAI;
using SK.Ext.Models;
using SK.Ext.Models.History;
using SK.Ext.Models.Result;

namespace SK.Ext.Sample;

public class CompletionAgentFullAnswerSample
{
    public static async Task Run(string groqKey)
    {
         OpenAIChatCompletionService chatCompletionService = new (
            modelId: "llama-3.3-70b-versatile",
            apiKey: groqKey,
            httpClient: new HttpClient { BaseAddress = new Uri("https://api.groq.com/openai/v1") }
        );

        var agent = new CompletionAgent(chatCompletionService);
        var history = new CompletionHistory
        {
            Messages =
            [
                new CompletionText
                {
                    Identity = AgentIdentity.User,
                    Content = "Write a detailed answer to the question: What is the capital of France? Include historical context, cultural significance, and any notable landmarks."
                }
            ]
        };
        var context = new CompletionContextBuilder()
            .WithSystemMessage(new CompletionSystemMessage
            {
                Prompt = "You are a helpful assistant that answers questions in detail. Once you have not provided a full answer, append the below:\n==== TO BE CONTINUED ====" +
                         "You will continue the answer in the next message."
            })
            .WithHistory(history).Build();
        bool isContinued;
        do
        {
            isContinued = false;
            await foreach (var content in agent.Completion(context, default))
            {
                if (CheckResult(content))
                {
                    isContinued = true;
                    history.Messages.Add(new CompletionText
                    {
                        Identity = AgentIdentity.Assistant,
                        Content = content is TextResult textResult ? textResult.Text : string.Empty
                    });
                    history.Messages.Add(new CompletionText
                    {
                        Identity = AgentIdentity.User,
                        Content = "Continue the answer."
                    });
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
