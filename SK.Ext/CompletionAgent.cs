using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace SK.Ext;

public interface ICompletionPlugin
{
    public Delegate FunctionDelegate { get; }
    public string PluginName { get; }
    public string Name { get; }
    public string FunctionDescription { get; }
    public IEnumerable<PluginParameter> Parameters { get; }
    public PluginReturnMetadata Return { get; }
    public bool IsRequired { get; }
}

public class PluginReturnMetadata
{
    public string Description { get; init; } = string.Empty;
    public Type? Type { get; init; } = null;
}

public abstract class PluginParameter
{
    public required string Name { get; init; }
}

public class ModelParameter : PluginParameter
{
    public string Description { get; init; } = string.Empty;
    public object? DefaultValue { get; init; } = null;
    public Type? Type { get; init; } = null;
    public bool IsRequired { get; init; } = false;
}

public class RuntimeParameter : PluginParameter
{
    public object? Value { get; init; } = null;
}

public class CompletionSettings
{
    public double Temperature { get; init; } = 0.7;
    public int MaxTokens { get; init; } = 1000;
    public double TopP { get; init; } = 1.0;
    public bool Stream { get; init; } = false;
    public bool Seed { get; init; } = false;
}

public class CompletionHistory
{
    public required List<ICompletionMessage> Messages { get; init; } = [];
}

public enum CompletionRole
{
    User,
    Assistant,
    System
}

public interface ICompletionMessage
{
    CompletionRole Role { get; }
    IDictionary<string, object>? Metadata { get; set; }
}

public class CompletionImage : ICompletionMessage
{
    public CompletionRole Role { get; set; }
    public ReadOnlyMemory<byte> Data { get; set; }
    public string? MimeType { get; set; }
    public IDictionary<string, object>? Metadata { get; set; } = new Dictionary<string, object>();
}

public class CompletionAudio : ICompletionMessage
{
    public CompletionRole Role { get; set; }
    public ReadOnlyMemory<byte> Data { get; set; }
    public string? MimeType { get; set; }
    public IDictionary<string, object>? Metadata { get; set; } = new Dictionary<string, object>();
}

public class CompletionText : ICompletionMessage
{
    public CompletionRole Role { get; set; }
    public string? Content { get; set; }
    public IDictionary<string, object>? Metadata { get; set; } = new Dictionary<string, object>();
}

public class CompletionFunctionCall : ICompletionMessage
{
    public CompletionRole Role { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PluginName { get; set; } = string.Empty;
    public IDictionary<string, object?> Arguments { get; set; } = new Dictionary<string, object?>();
    public IDictionary<string, object>? Metadata { get; set; } = new Dictionary<string, object>();
    public object? Result { get; set; } = null;
}

public class CompletionCollection : ICompletionMessage
{
    public CompletionRole Role { get; set; }
    public List<ICompletionMessage> Messages { get; } = [];
    public IDictionary<string, object>? Metadata { get; set; } = new Dictionary<string, object>();
}

public record CompletionContext(CompletionHistory History, CompletionSettings Settings, IEnumerable<ICompletionPlugin> Plugins);

public class CompletionContextBuilder
{
    private CompletionHistory _history = new()
    {
        Messages = [new CompletionText { Role = CompletionRole.System, Content = "You are a helpful assistant." }]
    };
    private CompletionSettings _settings = new();
    private IEnumerable<ICompletionPlugin> _plugins = [];

    public CompletionContextBuilder WithHistory(CompletionHistory history)
    {
        _history = history;
        return this;
    }

    public CompletionContextBuilder WithSettings(CompletionSettings settings)
    {
        _settings = settings;
        return this;
    }
    public CompletionContextBuilder WithPlugins(IEnumerable<ICompletionPlugin> plugins)
    {
        _plugins = plugins;
        return this;
    }

    public CompletionContext Build()
    {
        return new CompletionContext(_history, _settings, _plugins);
    }
}

public class CompletionAgent
{
    private readonly IChatCompletionService _chatCompletionService;

    public CompletionAgent(IChatCompletionService chatCompletionService)
    {
        _chatCompletionService = chatCompletionService;
    }

    public async IAsyncEnumerable<IContentResult> Completion(Kernel kernel, CompletionContext context,
        [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var content in Completion<object>(kernel, context, token))
        {
            yield return content;
        }
    }

    public async IAsyncEnumerable<IContentResult> Completion<T>(Kernel kernel, CompletionContext context,
        [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var content in InternalCompletion<T>(kernel, context, token))
        {
            yield return content;
        }
    }

    private async IAsyncEnumerable<IContentResult> InternalCompletion<T>(Kernel kernel, CompletionContext context,
        [EnumeratorCancellation] CancellationToken token)
    {
        var structuredOutput = typeof(T) != typeof(object);
        var chatHistory = MapCompletionHistoryToChatHistory(context.History);

        var k = kernel.Clone();

        var kernelFunctions = ImportPlugins(k, context.Plugins);
        var requiredToCallPlugins = context.Plugins.Where(x => x.IsRequired).ToList();
        var requiredToCall = kernelFunctions
            .Where(x => requiredToCallPlugins.Any(y => y.Name == x.Name && y.PluginName == x.PluginName));

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = context.Settings.Temperature,
            MaxTokens = context.Settings.MaxTokens,
            TopP = context.Settings.TopP,
            Seed = context.Settings.Seed ? GetSeed(chatHistory) : null,
            FunctionChoiceBehavior = requiredToCall.Any()
                ? FunctionChoiceBehavior.Required(requiredToCall, false)
                : FunctionChoiceBehavior.Auto(autoInvoke: false),
        };

        if (typeof(T) != typeof(object))
        {
#pragma warning disable SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
            executionSettings.ResponseFormat = typeof(T);
#pragma warning restore SKEXP0010 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        }

        var contentEnumerable = context.Settings.Stream
            ? _chatCompletionService.GetStreamingChatMessageContentsWithFunctions(k, chatHistory, executionSettings, token)
            : _chatCompletionService.GetChatMessageContentWithFunctions(k, chatHistory, executionSettings, token);

        var finalResult = new StringBuilder();

        await foreach (var content in contentEnumerable)
        {
            yield return content;
            if (structuredOutput && content is TextResult textContent)
            {
                finalResult.Append(textContent.Text);
            }

            if (content is FunctionCall functionCall && requiredToCall.Any())
            {
                requiredToCall = [.. requiredToCall.Where(x => x.PluginName == functionCall.PluginName
                    && x.Name == functionCall.Name)];
                executionSettings.FunctionChoiceBehavior = requiredToCall.Any()
                    ? FunctionChoiceBehavior.Required(requiredToCall, false)
                    : FunctionChoiceBehavior.Auto(autoInvoke: false);
            }
        }

        if (structuredOutput && finalResult.Length > 0)
        {
            var structuredResult = JsonSerializer.Deserialize<T>(finalResult.ToString());
            yield return new StructuredResult<T>()
            {
                Result = structuredResult,
                IsStreamed = context.Settings.Stream,
                CreatedAt = DateTime.UtcNow,
            };
        }
    }

    private static IEnumerable<KernelFunction> ImportPlugins(Kernel kernel, IEnumerable<ICompletionPlugin> plugins)
    {
        var groupPlugin = plugins.GroupBy(p => p.PluginName);
        foreach (var pluginGroup in groupPlugin)
        {
            var functions = pluginGroup
                .Select(completionPlugin => CreateKernelFunction(kernel, completionPlugin))
                .ToList();
            kernel.ImportPluginFromFunctions(pluginGroup.Key, functions);
        }
        return kernel.Plugins.SelectMany(x => x);
    }

    private static KernelFunction CreateKernelFunction(Kernel kernel, ICompletionPlugin completionPlugin)
    {
        var parameters = completionPlugin.Parameters.OfType<ModelParameter>()
            .Select(x => new KernelParameterMetadata(x.Name) { Description = x.Description, DefaultValue = x.DefaultValue, ParameterType = x.Type, IsRequired = x.IsRequired });
        var function = kernel.CreateFunctionFromMethod(
            completionPlugin.FunctionDelegate,
            completionPlugin.Name,
            completionPlugin.FunctionDescription,
            parameters,
            new KernelReturnParameterMetadata()
            {
                Description = completionPlugin.Return.Description,
                ParameterType = completionPlugin.Return.Type,
            });
        var runtimeParameters = completionPlugin.Parameters.OfType<RuntimeParameter>();
        if (runtimeParameters.Any())
        {
            var method = (Kernel kernel, KernelFunction currentFunction, KernelArguments arguments, CancellationToken cancellationToken) =>
            {
                foreach (var runtimeParameter in runtimeParameters)
                {
                    arguments.Add(runtimeParameter.Name, runtimeParameter.Value);
                }
                return function.InvokeAsync(kernel, arguments, cancellationToken);
            };
            var options = new KernelFunctionFromMethodOptions()
            {
                FunctionName = function.Name,
                Description = function.Description,
                Parameters = function.Metadata.Parameters,
                ReturnParameter = function.Metadata.ReturnParameter,
            };
            return KernelFunctionFactory.CreateFromMethod(method, options);
        }
        return function;
    }

    private static ChatHistory MapCompletionHistoryToChatHistory(CompletionHistory history)
    {
        var chatHistory = new ChatHistory();
        foreach (var message in history.Messages)
        {
            if (message is CompletionFunctionCall functionCall)
            {
                SimulateFunctionCall(chatHistory, functionCall);
            }
            else if (message is CompletionCollection completionCollection && completionCollection.Messages.All(x => x is not CompletionFunctionCall))
            {
                var functionCalls = completionCollection.Messages.OfType<CompletionFunctionCall>().Select(functionCall => new SimulatedFunctionCall
                {
                    Arguments = functionCall.Arguments,
                    FunctionName = functionCall.Name,
                    PluginName = functionCall.PluginName,
                    Result = functionCall.Result,
                });
                chatHistory.SimulateFunctionCalls(functionCalls);
            }
            else
            {
                chatHistory.AddMessage(
                    MapCompletionRoleToAuthorRole(message.Role),
                    MapCompletionMessageToKernelContent(message));
            }
        }
        return chatHistory;

        static void SimulateFunctionCall(ChatHistory chatHistory, CompletionFunctionCall functionCall)
        {
            var simulatedFunctionCall = new SimulatedFunctionCall
            {
                Arguments = functionCall.Arguments,
                FunctionName = functionCall.Name,
                PluginName = functionCall.PluginName,
                Result = functionCall.Result,
            };
            chatHistory.SimulateFunctionCalls([simulatedFunctionCall]);
        }
    }

    private static ChatMessageContentItemCollection MapCompletionMessageToKernelContent(ICompletionMessage message)
    {
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        return message switch
        {
            CompletionText textMessage => [new TextContent(textMessage.Content)],
            CompletionImage imageMessage => [new ImageContent(imageMessage.Data, imageMessage.MimeType)],
            CompletionAudio audioMessage => [new AudioContent(audioMessage.Data, audioMessage.MimeType)],
            CompletionCollection collectionMessage => [.. collectionMessage.Messages
                .Select(MapCompletionMessageToKernelContent).SelectMany(x => x)],
            _ => throw new NotSupportedException($"Unsupported message type: {message.GetType()}")
        };
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    }

    private static AuthorRole MapCompletionRoleToAuthorRole(CompletionRole role)
    {
        return role switch
        {
            CompletionRole.User => AuthorRole.User,
            CompletionRole.Assistant => AuthorRole.Assistant,
            CompletionRole.System => AuthorRole.System,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };
    }

    private static long GetSeed(ChatHistory chatHistory)
    {
        var hash = new HashCode();

        foreach (var message in chatHistory)
        {
            hash.Add(message.Role.ToString());

            if (message.Content is not null)
            {
                hash.Add(message.Content);
            }
            else
            {
                foreach (var item in message.Items)
                {
                    if (item is TextContent textContent)
                    {
                        hash.Add(textContent.Text);
                    }
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    else if (item is ImageContent imageContent)
                    {
                        if (imageContent.Data.HasValue)
                        {
                            hash.AddBytes(imageContent.Data.Value.Span);
                        }
                        hash.Add(imageContent.MimeType);
                    }
                    else if (item is AudioContent audioContent)
                    {
                        if (audioContent.Data.HasValue)
                        {
                            hash.AddBytes(audioContent.Data.Value.Span);
                        }
                        hash.Add(audioContent.MimeType);
                    }
#pragma warning restore SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                    else
                    {
                        throw new NotSupportedException($"Unsupported message type: {item.GetType()}");
                    }
                }
            }
        }

        // Convert the hash to a positive long value
        return Math.Abs((long)hash.ToHashCode());
    }
}
