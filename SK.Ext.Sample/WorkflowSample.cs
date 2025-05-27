using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SK.Ext.Sample;

/// <summary>
/// This sample demonstrates a workflow that determines the type of chat needed based on user input.
/// It uses the Semantic Kernel to create a chat assistant that can handle both generic and breakdown tasks.
/// The workflow consists of two main parts:
/// 1. Determine the chat type based on user input (GenericChat or BreakDownChat).
/// 2. Execute the appropriate chat type based on the determined type.
///   - For GenericChat, it engages in a general conversation.
///   - For BreakDownChat, it breaks down a task into clear steps and processes each step in parallel,
///     also includes a summarization step that generates a summary of the first three steps of the breakdown.
/// </summary>
public class WorkflowSample
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task Run(string groqKey, string userInput, CancellationToken cancellationToken = default)
    {
        var chatType = await DetermineChatType(groqKey, userInput, cancellationToken);
        Console.WriteLine($"Selected chat type: {chatType?.Type}");

        if (chatType?.Type == "GenericChat")
        {
            await ExecuteGenericChat(groqKey, userInput, cancellationToken);
        }
        else if (chatType?.Type == "BreakDownChat")
        {
            await ExecuteBreakDownChat(groqKey, userInput, cancellationToken);
        }
    }

    private static async Task<ChatTypeResult?> DetermineChatType(string groqKey, string userInput, CancellationToken cancellationToken)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion("llama-3.3-70b-versatile",
            new OpenAI.OpenAIClient(new ApiKeyCredential(groqKey),
            new OpenAI.OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") }));
        builder.Services.AddLogging(c => c.SetMinimumLevel(LogLevel.Trace));
        var kernel = builder.Build();

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();

        chatHistory.AddSystemMessage(@"You are an AI assistant that determines the type of chat needed based on user input.
            Select 'GenericChat' for general questions and conversations.
            Select 'BreakDownChat' when the user needs step-by-step explanations or task breakdowns.
            Respond with JSON in format: { ""type"": ""GenericChat"" } or { ""type"": ""BreakDownChat"" }");

        chatHistory.AddUserMessage(userInput);

#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var settings = new OpenAIPromptExecutionSettings { ResponseFormat = "json_object" };
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var result = new StringBuilder();

        await foreach (var content in chatCompletionService.GetChatMessageContentWithFunctions(kernel, chatHistory, settings, cancellationToken))
        {
            if (content is TextResult textResult)
            {
                result.Append(textResult.Text);
            }
        }

        return JsonSerializer.Deserialize<ChatTypeResult>(result.ToString(), Options);
    }

    private static async Task ExecuteGenericChat(string groqKey, string userInput, CancellationToken cancellationToken)
    {
        var kernel = BuildKernel(groqKey);
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();

        chatHistory.AddSystemMessage("You are a helpful AI assistant for general conversation.");
        chatHistory.AddUserMessage(userInput);

        PromptExecutionSettings settings = new();

        await foreach (var content in chatCompletionService.GetStreamingChatMessageContentsWithFunctions(kernel, chatHistory, settings, cancellationToken))
        {
            if (content is TextResult textResult && textResult.IsStreamed)
            {
                Console.Write(textResult.Text);
            }
        }
    }

    private record TaskBreakdown(List<TaskStep> Steps);
    private record TaskStep(int StepNumber, string Description);

    private static async Task ExecuteBreakDownChat(string groqKey, string userInput, CancellationToken cancellationToken)
    {
        var kernel = BuildKernel(groqKey);
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();

        chatHistory.AddSystemMessage(@"You are an AI assistant that breaks down tasks into clear steps.
            Return a JSON object with an array of steps, each containing a step number and description.
            Format: { ""steps"": [{ ""stepNumber"": 1, ""description"": ""First step"" }, ...] }");
        chatHistory.AddUserMessage($"Break down the following into clear steps: {userInput}");

#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var settings = new OpenAIPromptExecutionSettings { ResponseFormat = "json_object" };
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        var result = new StringBuilder();

        await foreach (var content in chatCompletionService.GetChatMessageContentWithFunctions(kernel, chatHistory, settings, cancellationToken))
        {
            if (content is TextResult textResult)
            {
                result.Append(textResult.Text);
            }
        }

        var breakdown = JsonSerializer.Deserialize<TaskBreakdown>(result.ToString(), Options);

        if (breakdown?.Steps == null || !breakdown.Steps.Any())
        {
            Console.WriteLine("No steps were generated.");
            return;
        }

        Console.WriteLine("\nExecuting steps in parallel:\n");

        var stepResults = new Dictionary<int, string>();
        var tasks = breakdown.Steps.Select(step =>
            (taskId: step.StepNumber, stream: ProcessStep(groqKey, step, cancellationToken))).ToList();

        await foreach (var (taskId, content) in MergeWithTaskId(tasks, cancellationToken: cancellationToken))
        {
            if (content is TextResult textResult)
            {
                Console.WriteLine($"Step {taskId}: {textResult.Text}");
                stepResults[taskId] = textResult.Text;
            }
        }

        // Add summarization step
        Console.WriteLine("\nGenerating summary of first three steps...\n");
        var summaryKernel = BuildKernel(groqKey);
        var summaryChatCompletionService = summaryKernel.GetRequiredService<IChatCompletionService>();
        var summaryChatHistory = new ChatHistory();

        var stepsContent = string.Join("\n", stepResults.OrderBy(x => x.Key).Take(3)
            .Select(x => $"Step {x.Key}: {x.Value}"));

        summaryChatHistory.AddSystemMessage("You are an AI assistant that provides concise summaries of multi-step processes.");
        summaryChatHistory.AddUserMessage($"Please provide a brief summary of this process:\n{stepsContent}");

        PromptExecutionSettings workflowSettings = new();

        await foreach (var content in summaryChatCompletionService.GetChatMessageContentWithFunctions(kernel, summaryChatHistory, workflowSettings, cancellationToken))
        {
            if (content is TextResult textResult)
            {
                Console.Write(textResult.Text);
            }
            if (content is CallingLLMExceptionResult exceptionResult)
            {
                Console.WriteLine($"[Exception] {exceptionResult.Exception.Message}");
            }
        }
    }

    private static async IAsyncEnumerable<(int taskId, T item)> MergeWithTaskId<T>(
        IEnumerable<(int taskId, IAsyncEnumerable<T> stream)> streams,
        int batchSize = 3,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chunks = streams.Chunk(batchSize);
        foreach (var chunk in chunks)
        {
            var chunkChannel = Channel.CreateUnbounded<(int taskId, T item)>();
            var chunkTasks = new List<Task>();

            foreach (var (taskId, stream) in chunk)
            {
                chunkTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await foreach (var item in stream.WithCancellation(cancellationToken))
                        {
                            await chunkChannel.Writer.WriteAsync((taskId, item), cancellationToken);
                        }
                    }
                    catch (Exception)
                    {
                        // Don't complete the channel writer on exception
                        // Just let the task fail and continue with other tasks
                        throw;
                    }
                }, cancellationToken));
            }

            _ = Task.WhenAll(chunkTasks).ContinueWith(t =>
            {
                // When Task.WhenAll completes, t.Exception will be an AggregateException
                // containing all the exceptions that were thrown by the tasks
                // If any task failed, t.Exception will not be null
                chunkChannel.Writer.TryComplete(t.Exception?.Flatten());
            }, cancellationToken);

            await foreach (var item in chunkChannel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return item;
            }
        }
    }

    private static async IAsyncEnumerable<IContentResult> ProcessStep(
        string groqKey,
        TaskStep step,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var kernel = BuildKernel(groqKey);
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var chatHistory = new ChatHistory();

        chatHistory.AddSystemMessage("You are an AI assistant processing a specific step of a task.");
        chatHistory.AddUserMessage($"Provide detailed information about how to accomplish this step: {step.Description}");

        PromptExecutionSettings settings = new();

        await foreach (var content in chatCompletionService.GetChatMessageContentWithFunctions(kernel, chatHistory, settings, cancellationToken))
        {
            yield return content;
        }
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

    private record ChatTypeResult(string Type);
}
