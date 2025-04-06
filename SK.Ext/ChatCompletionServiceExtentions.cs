using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;

namespace SK.Ext;

public interface IContentResult { }

public readonly struct TextResult : IContentResult
{
    public required string Text { get; init; }
}

public readonly struct FunctionCall : IContentResult
{
    public required string? Id { get; init; }
    public required string FunctionName { get; init; }
    public required IDictionary<string, object?>? Arguments { get; init; }
}

public readonly struct FunctionExecutionResult : IContentResult
{
    public required string? Id { get; init; }
    public required object? Result { get; init; }
    public required string? FunctionName { get; init; }
    public required string? PluginName { get; init; }
}

public readonly struct FunctionExceptionResult : IContentResult
{
    public required string? Id { get; init; }
    public required Exception Exception { get; init; }
}

public readonly struct UsageResult : IContentResult
{
    public required int OutputTokenCount { get; init; }
    public required int InputTokenCount { get; init; }
    public required int TotalTokenCount { get; init; }
    public required bool IsStreamed { get; init; }
}

public readonly struct IterationResult : IContentResult
{
    public required int Iteration { get; init; }
    public required bool IsStreamed { get; init; }
    public required FunctionCall[] FunctionCalls { get; init; }
}

public readonly struct CallingLLM : IContentResult
{
    public required bool IsStreamed { get; init; }
}

public readonly struct CallingLLMStreamedResult : IContentResult
{
    public required StreamingChatMessageContent Result { get; init; }
}

public readonly struct CallingLLMResult : IContentResult
{
    public required Microsoft.SemanticKernel.ChatMessageContent Result { get; init; }
}

public readonly struct CallingLLMExceptionResult : IContentResult
{
    public required Exception Exception { get; init; }
    public required bool IsStreamed { get; init; }
}

public static class ChatCompletionServiceExtentions
{
    private static async IAsyncEnumerable<IContentResult> GetStreamedChatCompletionResult(
        this IChatCompletionService chatCompletionService,
        ChatHistory chatHistory,
        PromptExecutionSettings settings,
        Kernel kernel,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var asyncEnumerable = chatCompletionService.GetStreamingChatMessageContentsAsync(chatHistory, settings, kernel, token);
        var enumerator = asyncEnumerable.GetAsyncEnumerator(token);
        try
        {
            while (true)
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                IContentResult result;
                try
                {
                    var moveNext = await enumerator.MoveNextAsync();

                    if (!moveNext)
                    {
                        break;
                    }
                    var streamingChatMessageContent = enumerator.Current;
                    result = new CallingLLMStreamedResult { Result = streamingChatMessageContent };
                }
                catch (Exception ex)
                {
                    result = new CallingLLMExceptionResult { Exception = ex, IsStreamed = true };
                }

                yield return result;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
    }

    public static async IAsyncEnumerable<IContentResult> GetStreamingChatMessageContentsWithFunctions(this IChatCompletionService chatCompletionService,
        Kernel kernel,
        ChatHistory chatHistory,
        PromptExecutionSettings settings,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var responseStringBuilder = new StringBuilder();
        var iteration = 0;
        while (true)
        {
            AuthorRole? authorRole = null;
            var fccBuilder = new FunctionCallContentBuilder();

            // TODO should we inform about max content exceeded?
            yield return new CallingLLM { IsStreamed = true };
            await foreach (var chatCompletionStreamedResult in chatCompletionService.GetStreamedChatCompletionResult(chatHistory, settings, kernel, token))
            {
                if (chatCompletionStreamedResult is CallingLLMStreamedResult streamingChatMessageContent)
                {
                    if (streamingChatMessageContent.Result.Content is not null)
                    {
                        responseStringBuilder.Append(streamingChatMessageContent.Result.Content);
                        yield return new TextResult { Text = streamingChatMessageContent.Result.Content };
                    }
                    authorRole ??= streamingChatMessageContent.Result.Role;
                    fccBuilder.Append(streamingChatMessageContent.Result);

                    if (streamingChatMessageContent.Result.Metadata is not null)
                    {
                        var outputTokens = GetOutputTokensFromMetadata(streamingChatMessageContent.Result.Metadata);
                        if (outputTokens is not null)
                        {
                            yield return new UsageResult
                            {
                                IsStreamed = true,
                                OutputTokenCount = outputTokens.OutputTokenCount,
                                InputTokenCount = outputTokens.InputTokenCount,
                                TotalTokenCount = outputTokens.TotalTokenCount
                            };
                        }
                    }
                }
                else if (chatCompletionStreamedResult is CallingLLMExceptionResult exceptionResult)
                {
                    yield return exceptionResult;
                    break;
                }
            }

            var functionCalls = fccBuilder.Build();
            if (!functionCalls.Any() && !IsEmptyResponse(responseStringBuilder))
            {
                yield return new IterationResult
                {
                    Iteration = iteration,
                    IsStreamed = true,
                    FunctionCalls = []
                };

                break;
            }

            if (authorRole is not null)
            {
                var fcContent = new Microsoft.SemanticKernel.ChatMessageContent(role: authorRole ?? default, content: null);
                chatHistory.Add(fcContent);

                foreach (var functionCall in functionCalls)
                {
                    fcContent.Items.Add(functionCall);
                    yield return new FunctionCall { FunctionName = functionCall.FunctionName, Id = functionCall.Id, Arguments = functionCall.Arguments };
                    // user can remove function call from chat history
                    // user removed all function calls
                    if (!chatHistory.Contains(fcContent)) break;
                    // user removed this function call
                    if (!fcContent.Items.Contains(functionCall)) continue;

                    FunctionResultContent functionResult;
                    try
                    {
                        functionResult = await functionCall.InvokeAsync(kernel, token);
                    }
                    catch (Exception ex)
                    {
                        functionResult = new FunctionResultContent(functionCall, ex);
                    }

                    chatHistory.Add(functionResult.ToChatMessage());
                    if (functionResult.Result is Exception exception)
                    {
                        yield return new FunctionExceptionResult { Id = functionResult.CallId, Exception = exception };
                    }
                    else
                    {
                        yield return new FunctionExecutionResult
                        {
                            Id = functionResult.CallId,
                            Result = functionResult.Result,
                            FunctionName = functionResult.FunctionName,
                            PluginName = functionResult.PluginName
                        };
                    }
                }

                if (functionCalls.Any())
                {
                    yield return new IterationResult
                    {
                        Iteration = iteration,
                        IsStreamed = true,
                        FunctionCalls = functionCalls.Select(fc => new FunctionCall { FunctionName = fc.FunctionName, Id = fc.Id, Arguments = fc.Arguments }).ToArray()
                    };

                    iteration += 1;
                    continue;
                }

                // not clear what to do with this case
                // go to the synchronous completion
            }

            if (!IsEmptyResponse(responseStringBuilder))
            {
                yield return new IterationResult
                {
                    Iteration = iteration,
                    IsStreamed = true,
                    FunctionCalls = functionCalls.Select(fc => new FunctionCall { FunctionName = fc.FunctionName, Id = fc.Id, Arguments = fc.Arguments }).ToArray()
                };
                break;
            }
            // something went wrong with streaming
            // fallback to the synchronous completion

            yield return new CallingLLM { IsStreamed = false };

            IContentResult result;
            try
            {
                result = new CallingLLMResult { Result = await chatCompletionService.GetChatMessageContentAsync(chatHistory, settings, kernel, token) };
            }
            catch (Exception ex)
            {
                result = new CallingLLMExceptionResult { Exception = ex, IsStreamed = false };
            }

            if (result is CallingLLMResult callingLLMResult)
            {
                var syncedCallResult = callingLLMResult.Result;
                if (syncedCallResult.Content is not null)
                {
                    yield return new TextResult { Text = syncedCallResult.Content };
                }
                if (syncedCallResult.Metadata is not null)
                {
                    var outputTokens = GetOutputTokensFromMetadata(syncedCallResult.Metadata);
                    if (outputTokens is not null)
                    {
                        yield return new UsageResult
                        {
                            IsStreamed = false,
                            OutputTokenCount = outputTokens.OutputTokenCount,
                            InputTokenCount = outputTokens.InputTokenCount,
                            TotalTokenCount = outputTokens.TotalTokenCount
                        };
                    }
                }

                var syncFunctionCalls = FunctionCallContent.GetFunctionCalls(syncedCallResult);
                if (!syncFunctionCalls.Any())
                {
                    break;
                }

                chatHistory.Add(syncedCallResult);

                foreach (FunctionCallContent functionCall in syncFunctionCalls)
                {
                    yield return new FunctionCall { FunctionName = functionCall.FunctionName, Id = functionCall.Id, Arguments = functionCall.Arguments };
                    // user can remove function call from chat history
                    // user removed all function calls
                    if (!chatHistory.Contains(syncedCallResult)) break;
                    // user removed this function call
                    if (!syncedCallResult.Items.Contains(functionCall)) continue;
                    FunctionResultContent functionResult;
                    try
                    {
                        functionResult = await functionCall.InvokeAsync(kernel, token);
                    }
                    catch (Exception ex)
                    {
                        functionResult = new FunctionResultContent(functionCall, ex);
                    }
                    chatHistory.Add(functionResult.ToChatMessage());
                    if (functionResult.Result is Exception exception)
                    {
                        yield return new FunctionExceptionResult { Id = functionResult.CallId, Exception = exception };
                    }
                    else
                    {
                        yield return new FunctionExecutionResult
                        {
                            Id = functionResult.CallId,
                            Result = functionResult.Result,
                            FunctionName = functionResult.FunctionName,
                            PluginName = functionResult.PluginName
                        }; ;
                    }
                }
                yield return new IterationResult
                {
                    Iteration = iteration,
                    IsStreamed = false,
                    FunctionCalls = syncFunctionCalls.Select(fc => new FunctionCall { FunctionName = fc.FunctionName, Id = fc.Id, Arguments = fc.Arguments }).ToArray()
                };

                iteration += 1;
            }
            if (result is CallingLLMExceptionResult callingLLMExceptionResult)
            {
                yield return callingLLMExceptionResult;
                yield return new IterationResult
                {
                    Iteration = iteration,
                    IsStreamed = false,
                    FunctionCalls = []
                };

                iteration += 1;
                break;
            }
        }
    }

    private static ChatTokenUsage? GetOutputTokensFromMetadata(IReadOnlyDictionary<string, object?>? metadata)
    {
        if (metadata is not null &&
            metadata.TryGetValue("Usage", out object? usageObject) &&
            usageObject is ChatTokenUsage usage)
        {
            return usage;
        }

        return null;
    }

    private static bool IsEmptyResponse(StringBuilder responseStringBuilder)
    {
        return string.IsNullOrEmpty(responseStringBuilder.ToString().Trim());
    }
}
