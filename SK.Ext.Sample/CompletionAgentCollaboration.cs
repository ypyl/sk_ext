using System.Text;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SK.Ext.Models;
using SK.Ext.Models.History;
using SK.Ext.Models.Result;

namespace SK.Ext.Sample;

public class CompletionAgentTwoAgents
{
    public static async Task Run(string groqKey, CancellationToken cancellationToken = default)
    {
        OpenAIChatCompletionService chatCompletionService = new(
           modelId: "llama-3.3-70b-versatile",
           apiKey: groqKey,
           httpClient: new HttpClient { BaseAddress = new Uri("https://api.groq.com/openai/v1") }
       );

        var agent = new CompletionAgent(chatCompletionService);

        var writerSystemMessage = @"You are a Writer AI. Your role is to write clear, comprehensive responses to user requests.
            Focus on accuracy, clarity, and addressing all aspects of the request. Consider previous conversation context when responding.";
        var writerIdentity = new AgentIdentity { Name = "writer", Role = CompletionRole.Assistant };
        var reviewerSystemMessage = @"You are a Reviewer AI. Your role is to review the writer's content and provide feedback.
            If the content is satisfactory, respond with 'APPROVED: ' followed by a brief explanation.
            If changes are needed, provide specific suggestions for improvement. Consider the entire conversation context. NEVER ACCEPT THE FIRST VERSION.";
        var reviewerIdentity = new AgentIdentity { Name = "rewiewer", Role = CompletionRole.Assistant };
        var finalizerSystemMessage = @"You are a Finalizator AI. Your role is to create a final, polished version based on the entire conversation history.
            Incorporate the best elements from the discussion and ensure the final response is comprehensive and well-structured.";
        var finalizerIdentity = new AgentIdentity { Name = "finalizer", Role = CompletionRole.Assistant };
        var history = new CompletionHistory("Explain the SOLID principles in software development with examples.");
        var context = new CompletionContextBuilder().WithHistory(history).Build();

        context = context.ForAgent(writerIdentity, writerSystemMessage);
        context = await RunAgent(agent, writerIdentity, context, cancellationToken);

        var iteration = 0;
        const int maxIterations = 5;

        while (iteration < maxIterations)
        {
            context = context.ForAgent(reviewerIdentity, reviewerSystemMessage);
            context = await RunAgent(agent, reviewerIdentity, context, cancellationToken);

            if (context.History.Messages.OfType<CompletionText>().Last(x => x.Identity == reviewerIdentity).Content.StartsWith("APPROVED:", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("\nReviewer approved the content!");
                break;
            }

            context = context.ForAgent(writerIdentity, writerSystemMessage);
            context = await RunAgent(agent, writerIdentity, context, cancellationToken);

            iteration++;
        }

        context = context.ForAgent(finalizerIdentity, finalizerSystemMessage);
        await RunAgent(agent, finalizerIdentity, context, cancellationToken);
    }

    private static async Task<CompletionContext> RunAgent(CompletionAgent agent, AgentIdentity agentIdentity, CompletionContext context, CancellationToken cancellationToken)
    {
        var result = new StringBuilder();
        await foreach (var content in agent.Completion(context, cancellationToken))
        {
            if (content is TextResult textResult)
            {
                result.Append(textResult.Text);
            }
            if (content is TextResult streamedTextContent && streamedTextContent.IsStreamed)
            {
                result.Append(streamedTextContent.Text);
            }
        }
        var agentAnswer = result.ToString();
        Console.WriteLine($"[{agentIdentity.Name}]: {agentAnswer}");
        return context with
        {
            History = new CompletionHistory
            {
                Messages = [
                    .. context.History.Messages,
                    new CompletionText
                    {
                        Identity = agentIdentity,
                        Content = agentAnswer
                    }
                ]
            }
        };
    }
}
