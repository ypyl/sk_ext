using System.ClientModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SK.Ext;

var builder = Kernel.CreateBuilder();
// builder.AddOpenAIChatCompletion("llama-3.3-70b-versatile",
//     new OpenAI.OpenAIClient(new ApiKeyCredential("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"), new OpenAI.OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") }));
#pragma warning disable SKEXP0070
builder.AddOllamaChatCompletion("phi4-mini", new Uri("http://localhost:11434/"));
#pragma warning restore SKEXP0070
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

await foreach (var content in chatCompletionService.StreamChatMessagesWithFunctions(kernel, chatHistory, settings))
{
    Console.Write(content switch
    {
        TextResult streamedTextResult
            => $"{streamedTextResult.Text}",

        CallingLLM callingLLM
            => $"[Calling LLM] Streamed: {callingLLM.IsStreamed}\n",

        FunctionCall functionCall
            => $"[Function Call] {functionCall.FunctionName}\n" +
               string.Join("\n", (functionCall.Arguments ?? new Dictionary<string, object?>()).Select(arg => $"{arg.Key}: {arg.Value}")) +
               (functionCall.Arguments?.Any() == true ? "\n" : string.Empty),

        FunctionExceptionResult functionExceptionResult
            => $"[Exception] {functionExceptionResult.Exception.Message}\n",

        FunctionExecutionResult functionResult
            => $"[Result] {functionResult.Result?.ToString() ?? string.Empty}\n",

        UsageResult usageResult
            => $"[Usage] Input Tokens: {usageResult.InputTokenCount}, Output Tokens: {usageResult.OutputTokenCount}, Total Tokens: {usageResult.TotalTokenCount}\n",

        IterationResult iterationResult
            => $"[Iteration] Iteration: {iterationResult.Iteration}, Is Streamed: {iterationResult.IsStreamed}, Function Calls: {string.Join(", ", iterationResult.FunctionCalls.Select(fc => fc.FunctionName))}\n",

        _ => string.Empty
    });

    if (content is FunctionExecutionResult fr)
    {
        if (fr.FunctionName == "GetWeatherForCity")
        {
            chatHistory.ReplaceFunctionCallResult(fr.Id, "70 and sunny");
        }
    }
}
