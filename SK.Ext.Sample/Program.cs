using System.ClientModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SK.Ext;

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
chatHistory.AddUserMessage("What is the weather in Boston today?");

PromptExecutionSettings settings = new() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false) };

var taskId = 0;

await foreach (var content in chatCompletionService.StreamChatMessagesWithFunctions(kernel, chatHistory, settings))
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
            => $"[Calling LLM Exception {taskId}] {callingLLMExceptionResult.Exception.Message}\n",
        _ => string.Empty
    });

    if (content is FunctionExecutionResult fr)
    {
        if (fr.FunctionName == "GetWeatherForCity" && taskId == 0)
        {
            chatHistory.ReplaceFunctionCallResult(fr.Id, "50 and sunny");
        }
    }
}
