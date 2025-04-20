using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ClientModel;
using System.Text;

namespace SK.Ext.Sample;

public class CollaborationSample
{
    private record ChatResponse(string Content, bool IsApproved = false);

    public static async Task Run(string groqKey, string userInput, CancellationToken cancellationToken = default)
    {
        var kernel = BuildKernel(groqKey);
        var chatService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new List<(string role, string content)>();

        var writerResponse = await GetWriterResponse(chatService, kernel, userInput, history, cancellationToken);
        history.Add(("writer", writerResponse.Content));

        var iteration = 0;
        const int maxIterations = 5;

        while (iteration < maxIterations)
        {
            var reviewerResponse = await GetReviewerResponse(chatService, kernel, writerResponse.Content, history, cancellationToken);
            history.Add(("reviewer", reviewerResponse.Content));

            if (reviewerResponse.IsApproved)
            {
                Console.WriteLine("\nReviewer approved the content!");
                break;
            }

            writerResponse = await GetWriterResponse(chatService, kernel, reviewerResponse.Content, history, cancellationToken);
            history.Add(("writer", writerResponse.Content));

            iteration++;
        }

        var finalResponse = await GetFinalizatorResponse(chatService, kernel, history, cancellationToken);
        Console.WriteLine("\nFinal Response:");
        Console.WriteLine(finalResponse);
    }

    private static void AddHistory(ChatHistory chatHistory, List<(string role, string content)> history, string assistantRole)
    {
        foreach (var (role, content) in history)
        {
            if (role == assistantRole)
            {
                chatHistory.AddAssistantMessage(content);
            }
            else
            {
                chatHistory.AddUserMessage(content);
            }
        }
    }

    private static async Task<ChatResponse> GetWriterResponse(IChatCompletionService chatService, Kernel kernel, string prompt, List<(string role, string content)> history, CancellationToken cancellationToken)
    {
        Console.WriteLine("\nWriter is working...");
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(@"You are a Writer AI. Your role is to write clear, comprehensive responses to user requests.
            Focus on accuracy, clarity, and addressing all aspects of the request. Consider previous conversation context when responding.");

        AddHistory(chatHistory, history, "writer");

        chatHistory.AddUserMessage(prompt);

        var response = await GetChatResponse(chatService, kernel, chatHistory, cancellationToken);
        Console.WriteLine("\nWriter's response:");
        Console.WriteLine(response);
        return new ChatResponse(response);
    }

    private static async Task<ChatResponse> GetReviewerResponse(IChatCompletionService chatService, Kernel kernel, string writerContent, List<(string role, string content)> history, CancellationToken cancellationToken)
    {
        Console.WriteLine("\nReviewer is analyzing...");
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(@"You are a Reviewer AI. Your role is to review the writer's content and provide feedback.
            If the content is satisfactory, respond with 'APPROVED: ' followed by a brief explanation.
            If changes are needed, provide specific suggestions for improvement. Consider the entire conversation context. NEVER ACCEPT THE FIRST VERSION.");
        AddHistory(chatHistory, history, "reviewer");
        chatHistory.AddUserMessage($"Please review this content:\n{writerContent}");

        var response = await GetChatResponse(chatService, kernel, chatHistory, cancellationToken);
        Console.WriteLine("\nReviewer's response:");
        Console.WriteLine(response);
        return new ChatResponse(response, response.StartsWith("APPROVED:"));
    }

    private static async Task<string> GetFinalizatorResponse(IChatCompletionService chatService, Kernel kernel, List<(string role, string content)> history, CancellationToken cancellationToken)
    {
        Console.WriteLine("\nFinalizator is preparing the final version...");
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(@"You are a Finalizator AI. Your role is to create a final, polished version based on the entire conversation history.
            Incorporate the best elements from the discussion and ensure the final response is comprehensive and well-structured.");

        var conversationSummary = new StringBuilder();
        foreach (var (role, content) in history)
        {
            conversationSummary.AppendLine($"{role.ToUpperInvariant()}:\n{content}\n");
        }

        chatHistory.AddUserMessage($"Please create a final version based on this conversation:\n{conversationSummary}");

        return await GetChatResponse(chatService, kernel, chatHistory, cancellationToken);
    }

    private static async Task<string> GetChatResponse(IChatCompletionService chatService, Kernel kernel, ChatHistory chatHistory, CancellationToken cancellationToken)
    {
        var result = new StringBuilder();
        await foreach (var content in chatService.GetChatMessageContentWithFunctions(kernel, chatHistory, new PromptExecutionSettings(), cancellationToken))
        {
            if (content is TextResult textResult)
            {
                result.Append(textResult.Text);
            }
            if (content is StreamedTextResult streamedTextContent)
            {
                result.Append(streamedTextContent.Text);
            }
        }
        return result.ToString();
    }

    private static Kernel BuildKernel(string groqKey)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion("llama-3.3-70b-versatile",
            new OpenAI.OpenAIClient(new ApiKeyCredential(groqKey),
            new OpenAI.OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") }));
        builder.Services.AddLogging(c => c.SetMinimumLevel(LogLevel.Trace));
        return builder.Build();
    }
}
