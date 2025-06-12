using System.Text;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OllamaSharp;
using SK.Ext.Models;
using SK.Ext.Models.History;
using SK.Ext.Models.Result;

namespace SK.Ext.Sample;

public class AgentCollaboration
{
    public static async Task Run(string groqKey, CancellationToken cancellationToken = default)
    {
        using var ollamaClient = new OllamaApiClient(
            uriString: "http://localhost:11434",    // E.g. "http://localhost:11434" if Ollama has been started in docker as described above.
            defaultModel: "gemma3:4b" // E.g. "phi3" if phi3 was downloaded as described above.
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

        var writerSystemMessage = @"You are a Writer AI. Your role is to write clear, comprehensive responses to user requests.
            Focus on accuracy, clarity, and addressing all aspects of the request. Consider previous conversation context when responding.";
        var writerIdentity = new AgentIdentity { Name = "writer", Role = CompletionRole.Assistant };
        var reviewerSystemMessage = @"You are a Reviewer AI. Your role is to review the writer's content and provide feedback.
            If the content is satisfactory, respond with 'APPROVED: ' followed by a brief explanation.
            If changes are needed, provide short one sentence specific suggestion for improvement.";
        var reviewerIdentity = new AgentIdentity { Name = "rewiewer", Role = CompletionRole.Assistant };
        var finalizerSystemMessage = @"You are a Finalizator AI. Your role is to create a final, polished version based on the entire conversation history.
            Incorporate the best elements from the discussion and ensure the final response is comprehensive and well-structured.";
        var finalizerIdentity = new AgentIdentity { Name = "finalizer", Role = CompletionRole.Assistant };
        var context = new CompletionContextBuilder().WithInitialUserMessage("Explain the SOLID principles in software development with examples.").Build();

        context = await RunAgent(runtime, writerIdentity, context, writerSystemMessage, cancellationToken);

        var iteration = 0;
        const int maxIterations = 5;

        while (iteration < maxIterations)
        {
            context = await RunAgent(runtime, reviewerIdentity, context, reviewerSystemMessage, cancellationToken);

            if (context.History.Messages.OfType<CompletionText>().Last(x => x.Identity == reviewerIdentity).Content.StartsWith("APPROVED:", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("\nReviewer approved the content!");
                break;
            }

            context = await RunAgent(runtime, writerIdentity, context, writerSystemMessage, cancellationToken);

            iteration++;
        }

        context = await RunAgent(runtime, finalizerIdentity, context, finalizerSystemMessage, cancellationToken);
    }

    private static async Task<CompletionContext> RunAgent(CompletionRuntime runtime, AgentIdentity agentIdentity, CompletionContext context, string systemMessage, CancellationToken cancellationToken)
    {
        // Ensure the context is set for the agent with the system message
        context = context.SwitchIdentity(agentIdentity, systemMessage);
        var result = new StringBuilder();
        await foreach (var content in runtime.Completion(context, cancellationToken))
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
        return context.AddMessages([new CompletionText
        {
            Identity = agentIdentity,
            Content = agentAnswer
        }]);
    }
}
