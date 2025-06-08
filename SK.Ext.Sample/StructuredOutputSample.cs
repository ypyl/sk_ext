using System.ClientModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SK.Ext.Models.Result;

namespace SK.Ext.Sample;

public class StructuredOutputSample
{
    private static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    public static async Task Run(string groqKey, CancellationToken cancellationToken = default)
    {
        var completeResult = new StringBuilder();
        await foreach (var result in FetchCityPopulationInfo(groqKey, cancellationToken))
        {
            if (result is TextResult textResult)
            {
                completeResult.Append(textResult.Text);
                Console.WriteLine($"[TextResult] {textResult.Text}");
            }
            if (result is CallingLLMExceptionResult exceptionResult)
            {
                Console.WriteLine($"[Exception] {exceptionResult.Exception.Message}");
            }
        }

        var parsed = JsonSerializer.Deserialize<CityName>(completeResult.ToString(), options);
        Console.WriteLine($"[Parsed] {parsed?.Name}");
    }

    private static async IAsyncEnumerable<IContentResult> FetchCityPopulationInfo(string groqKey, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion("llama-3.3-70b-versatile",
            // Sample groq API key (revoked), replace with your own
            new OpenAI.OpenAIClient(new ApiKeyCredential(groqKey), new OpenAI.OpenAIClientOptions { Endpoint = new Uri("https://api.groq.com/openai/v1") }));
        builder.Services.AddLogging(c => c.SetMinimumLevel(LogLevel.Trace));
        Kernel kernel = builder.Build();

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        ChatHistory chatHistory = [];
        chatHistory.AddSystemMessage("You are AI assistant to select the biggest city by population from the list and return JSON object with the city name, e.g. { \"name\": \"Toronto\" }.");
        chatHistory.AddUserMessage($"Please provide the biggest city by population from the list: Warsaw, London, Paris, Berlin, Madrid.");

#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        OpenAIPromptExecutionSettings settings = new() { ResponseFormat = "json_object" };
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

        await foreach (var content in chatCompletionService.GetChatMessageContentWithFunctions(kernel, chatHistory, settings, cancellationToken))
        {
            yield return content;
        }
    }

    private record CityName(string Name);
}
