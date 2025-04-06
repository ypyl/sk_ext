using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SK.Ext;

await foreach (var (taskId, content) in MergeWithTaskId([
    (1, SetUpWeatherAssistantKernel("Boston", "25 and snowing")),
    (2, SetUpWeatherAssistantKernel("London", "15 and foggy")),
    (3, SetUpWeatherAssistantKernel("Miami", "95 and humid")),
    (4, SetUpWeatherAssistantKernel("Paris", "70 and partly cloudy")),
    (5, SetUpWeatherAssistantKernel("Tokyo", "85 and stormy")),
    (6, SetUpWeatherAssistantKernel("Sydney", "65 and windy")),
    (7, SetUpWeatherAssistantKernel("Tel Aviv", "90 and clear"))]))
{
    ProcessingStreamResults(content, taskId);
}

static void ProcessingStreamResults(IContentResult content, int taskId = 0)
{
    Console.Write(content switch
    {
        TextResult streamedTextResult
            => $"{streamedTextResult.Text}",

        CallingLLM callingLLM
            => $"[Calling LLM {taskId}] Streamed: {callingLLM.IsStreamed}\n",

        FunctionCall functionCall
            => $"[Function Call {taskId}] {functionCall.FunctionName}\n" +
               string.Join("\n", (functionCall.Arguments ?? new Dictionary<string, object?>()).Select(arg => $"{arg.Key}: {arg.Value}")) +
               (functionCall.Arguments?.Any() == true ? "\n" : string.Empty),

        FunctionExceptionResult functionExceptionResult
            => $"[Exception {taskId}] {functionExceptionResult.Exception.Message}\n",

        FunctionExecutionResult functionResult
            => $"[Result {taskId}] {functionResult.Result?.ToString() ?? string.Empty}\n",

        UsageResult usageResult
            => $"[Usage {taskId}] Input Tokens: {usageResult.InputTokenCount}, Output Tokens: {usageResult.OutputTokenCount}, Total Tokens: {usageResult.TotalTokenCount}\n",

        IterationResult iterationResult
            => $"[Iteration {taskId}] Iteration: {iterationResult.Iteration}, Is Streamed: {iterationResult.IsStreamed}, Function Calls: {string.Join(", ", iterationResult.FunctionCalls.Select(fc => fc.FunctionName))}\n",

        CallingLLMExceptionResult callingLLMExceptionResult
            => $"[Calling LLM Exception {taskId}] {callingLLMExceptionResult.Exception}\n",
        _ => string.Empty
    });
}

static async IAsyncEnumerable<IContentResult> SetUpWeatherAssistantKernel(string city, string weatherCondition = "31 and snowing", [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var builder = Kernel.CreateBuilder();
    builder.AddOpenAIChatCompletion("llama-3.3-70b-versatile",
        new OpenAI.OpenAIClient(new ApiKeyCredential("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"), new OpenAI.OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") }));
    builder.Services.AddLogging(c => c.SetMinimumLevel(LogLevel.Trace));
    Kernel kernel = builder.Build();

    kernel.ImportPluginFromFunctions("HelperFunctions",
    [
        kernel.CreateFunctionFromMethod(() => new List<string> { "Squirrel Steals Show", "Dog Wins Lottery" }, "GetLatestNewsTitles", "Retrieves latest news titles."),
        kernel.CreateFunctionFromMethod(() => DateTime.UtcNow.ToString("R"), "GetCurrentDateTimeInUtc", "Retrieves the current date time in UTC."),
        kernel.CreateFunctionFromMethod((string cityName, string currentDateTimeInUtc) =>
            cityName switch
            {
                "Boston" => "61 and rainy",
                "London" => "55 and cloudy",
                "Miami" => "80 and sunny",
                "Paris" => "60 and rainy",
                "Tokyo" => "50 and sunny",
                "Sydney" => "75 and sunny",
                "Tel Aviv" => "80 and sunny",
                _ => "31 and snowing",
            }, "GetWeatherForCity", "Gets the current weather for the specified city and specified date time."),
    ]);

    var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

    ChatHistory chatHistory = [];
    chatHistory.AddSystemMessage("You are AI weather assistant.");
    chatHistory.AddUserMessage($"What is the weather in {city} today?");

    PromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false) };

    await foreach (var content in chatCompletionService.GetStreamingChatMessageContentsWithFunctions(kernel, chatHistory, settings, cancellationToken))
    {
        yield return content;
        if (content is FunctionExecutionResult fr)
        {
            if (fr.FunctionName == "GetWeatherForCity")
            {
                chatHistory.ReplaceFunctionCallResult(fr.Id, weatherCondition);
            }
        }
    }
}

static async IAsyncEnumerable<(int taskId, T item)> MergeWithTaskId<T>(
        IEnumerable<(int taskId, IAsyncEnumerable<T> stream)> streams,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var channel = Channel.CreateUnbounded<(int taskId, T item)>();

    var tasks = new List<Task>();

    foreach (var (taskId, stream) in streams)
    {
        tasks.Add(Task.Run(async () =>
        {
            try
            {
                await foreach (var item in stream.WithCancellation(cancellationToken))
                {
                    await channel.Writer.WriteAsync((taskId, item), cancellationToken);
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

    _ = Task.WhenAll(tasks).ContinueWith(t =>
    {
        // When Task.WhenAll completes, t.Exception will be an AggregateException
        // containing all the exceptions that were thrown by the tasks
        // If any task failed, t.Exception will not be null
        if (t.Exception != null)
        {
            // We have at least one exception from the tasks
            channel.Writer.TryComplete(t.Exception.Flatten());
        }
        else
        {
            // All tasks completed successfully
            channel.Writer.TryComplete();
        }
    }, cancellationToken);

    await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
    {
        yield return item;
    }
}
