using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using SK.Ext.Models;
using SK.Ext.Models.History;
using SK.Ext.Models.Plugin;
using SK.Ext.Models.Result;

namespace SK.Ext;

// TODO allow to pass default set of plugins to the constructor
public class CompletionRuntime(IChatCompletionService chatCompletionService) : ICompletionRuntime
{
    private readonly IChatCompletionService _chatCompletionService = chatCompletionService;
    private readonly Kernel _kernel = Kernel.CreateBuilder().Build();
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public async IAsyncEnumerable<IContentResult> Completion(CompletionContext context,
        [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var content in Completion<object>(context, token))
        {
            yield return content;
        }
    }

    public async IAsyncEnumerable<IContentResult> Completion<T>(CompletionContext context,
        [EnumeratorCancellation] CancellationToken token)
    {
        await foreach (var content in InternalCompletion<T>(context, token))
        {
            yield return content;
        }
    }

    private async IAsyncEnumerable<IContentResult> InternalCompletion<T>(CompletionContext context,
        [EnumeratorCancellation] CancellationToken token)
    {
        var k = _kernel.Clone();
        var structuredOutput = typeof(T) != typeof(object);
        var chatHistory = MapCompletionHistoryToChatHistory(context.SystemMessage, context.History);

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
            executionSettings.ResponseFormat = typeof(T);
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
                requiredToCall = [.. requiredToCall.Where(x => x.PluginName != functionCall.PluginName || x.Name != functionCall.Name)];
                executionSettings.FunctionChoiceBehavior = requiredToCall.Any()
                    ? FunctionChoiceBehavior.Required(requiredToCall, false)
                    : FunctionChoiceBehavior.Auto(autoInvoke: false);
            }
        }

        if (structuredOutput && finalResult.Length > 0)
        {
            var structuredResult = JsonSerializer.Deserialize<T>(finalResult.ToString(), _options);
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

    private static ChatHistory MapCompletionHistoryToChatHistory(CompletionSystemMessage systemMessage, CompletionHistory history)
    {
        var chatHistory = new ChatHistory(systemMessage.Prompt);
        foreach (var message in history)
        {
            if (message is CompletionFunctionCall functionCall)
            {
                AddFunctionCallsToChatHistory(chatHistory, [functionCall]);
            }
            else if (message is CompletionCollection completionCollection && completionCollection.Messages.All(x => x is not CompletionFunctionCall))
            {
                var functionCalls = completionCollection.Messages.OfType<CompletionFunctionCall>();
                AddFunctionCallsToChatHistory(chatHistory, functionCalls);
            }
            else
            {
                chatHistory.AddMessage(
                    MapCompletionRoleToAuthorRole(message.Identity),
                    MapCompletionMessageToKernelContent(message));
            }
        }
        return chatHistory;

        static void AddFunctionCallsToChatHistory(ChatHistory chatHistory, IEnumerable<CompletionFunctionCall> functionCalls)
        {
            var simulatedFunctionCalls = functionCalls.Select(functionCall => new SimulatedFunctionCall
            {
                Arguments = functionCall.Arguments,
                FunctionName = functionCall.Name,
                PluginName = functionCall.PluginName,
                Result = functionCall.Result,
            });
            chatHistory.SimulateFunctionCalls(simulatedFunctionCalls);
        }
    }

    private static ChatMessageContentItemCollection MapCompletionMessageToKernelContent(CompletionMessage message)
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

    private static AuthorRole MapCompletionRoleToAuthorRole(ParticipantIdentity identity)
    {
        return identity.Role switch
        {
            CompletionRole.User => AuthorRole.User,
            CompletionRole.Assistant => AuthorRole.Assistant,
            _ => throw new NotSupportedException($"Unsupported role: {identity.Role}")
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
