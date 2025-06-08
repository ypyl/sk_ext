using System.ClientModel;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SK.Ext.Models.Result;

namespace SK.Ext.Sample;

public class ParallelExecutionSample
{
    public static async Task Run(string groqKey)
    {
        IEnumerable<(int taskId, IAsyncEnumerable<IContentResult> stream)> tasksToParalel = [
            (1, SetUpWeatherAssistantKernel(groqKey, "Boston", "25 and snowing")),
            (2, SetUpWeatherAssistantKernel(groqKey, "London", "15 and foggy")),
            (3, SetUpWeatherAssistantKernel(groqKey, "Miami", "95 and humid")),
            (4, SetUpWeatherAssistantKernel(groqKey, "Paris", "70 and partly cloudy")),
            (5, SetUpWeatherAssistantKernel(groqKey, "Tokyo", "85 and stormy")),
            (6, SetUpWeatherAssistantKernel(groqKey, "Sydney", "65 and windy")),
            (7, SetUpWeatherAssistantKernel(groqKey, "Tel Aviv", "90 and clear"))
        ];
        await foreach (var (taskId, content) in tasksToParalel.MergeWithTaskId())
        {
            ProcessingStreamResults(content, taskId);
        }
    }

    private static void ProcessingStreamResults(IContentResult content, int taskId = 0)
    {
        Console.Write(content switch
        {
            TextResult t when !t.IsStreamed
                => $"[Text Result {taskId}] {t.Text}",

            TextResult t when t.IsStreamed
                => $"{t.Text}",

            CallingLLM callingLLM
                => $"[Calling LLM {taskId}] Streamed: {callingLLM.IsStreamed}\n",

            FunctionCall functionCall
                => $"[Function Call {taskId}] {functionCall.Name}\n" +
                   string.Join("\n", (functionCall.Arguments ?? new Dictionary<string, object?>()).Select(arg => $"{arg.Key}: {arg.Value}")) +
                   (functionCall.Arguments?.Any() == true ? "\n" : string.Empty),

            FunctionExceptionResult functionExceptionResult
                => $"[Exception {taskId}] {functionExceptionResult.Exception.Message}\n",

            FunctionExecutionResult functionResult
                => $"[Result {taskId}] {functionResult.Result?.ToString() ?? string.Empty}\n",

            UsageResult usageResult
                => $"[Usage {taskId}] Input Tokens: {usageResult.InputTokenCount}, Output Tokens: {usageResult.OutputTokenCount}, Total Tokens: {usageResult.TotalTokenCount}\n",

            IterationResult iterationResult
                => $"[Iteration {taskId}] Iteration: {iterationResult.Iteration}, Is Streamed: {iterationResult.IsStreamed}, Function Calls: {string.Join(", ", iterationResult.CalledFullFunctions.Select(fc => fc.FunctionName))}\n",

            CallingLLMExceptionResult callingLLMExceptionResult
                => $"[Calling LLM Exception {taskId}] {callingLLMExceptionResult.Exception}\n",
            _ => string.Empty
        });
    }

    private static async IAsyncEnumerable<IContentResult> SetUpWeatherAssistantKernel(string groqKey, string city, string weatherCondition = "31 and snowing", [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion("llama-3.3-70b-versatile",
            // Sample groq API key (revoked), replace with your own
            new OpenAI.OpenAIClient(new ApiKeyCredential(groqKey), new OpenAI.OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") }));
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
                if (fr.Name == "GetWeatherForCity")
                {
                    chatHistory.ReplaceFunctionCallResult(fr.Id, weatherCondition);
                }
            }
        }
    }


}
