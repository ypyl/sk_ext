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

public readonly struct StreamedFunctionExecutionResult : IContentResult
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
    public required bool IsEmptyResponse { get; init; }
    public required bool IsError { get; init; }
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
        var iteration = 0;
        while (true)
        {
            yield return new CallingLLM { IsStreamed = true };

            var isFallbackToSync = false;
            var isContinue = false;

            await foreach (var chatCompletionStreamedResult in chatCompletionService.GetStreamingChatMessageContentsWithFunctionsInternal(kernel, chatHistory, settings, iteration, token))
            {
                if (chatCompletionStreamedResult is IterationResult iterationResult)
                {
                    isFallbackToSync = !iterationResult.IsError && iterationResult.IsEmptyResponse;
                    isContinue = iterationResult.FunctionCalls.Length > 0;
                }
                yield return chatCompletionStreamedResult;
            }
            if (isContinue)
            {
                iteration += 1;
                continue;
            }
            if (!isFallbackToSync)
            {
                break;
            }

            // something went wrong with streaming
            // fallback to the synchronous completion

            yield return new CallingLLM { IsStreamed = false };

            await foreach (var chatCompletionResult in chatCompletionService.GetChatMessageContentWithFunctionsInternal(kernel, chatHistory, settings, iteration, token))
            {
                if (chatCompletionResult is IterationResult iterationResult)
                {
                    isContinue = iterationResult.FunctionCalls.Length > 0;
                }
                yield return chatCompletionResult;
            }
            if (isContinue)
            {
                iteration += 1;
                continue;
            }
            break;
        }
    }

    public static async IAsyncEnumerable<IContentResult> GetChatMessageContentWithFunctions(this IChatCompletionService chatCompletionService,
        Kernel kernel,
        ChatHistory chatHistory,
        PromptExecutionSettings settings,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var iteration = 0;
        while (true)
        {
            var isContinue = false;
            await foreach (var chatCompletionResult in chatCompletionService.GetChatMessageContentWithFunctionsInternal(kernel, chatHistory, settings, iteration, token))
            {
                if (chatCompletionResult is IterationResult iterationResult)
                {
                    isContinue = iterationResult.FunctionCalls.Length > 0;
                }
                yield return chatCompletionResult;
            }
            if (isContinue)
            {
                iteration += 1;
                continue;
            }
            break;
        }
    }

    private static async IAsyncEnumerable<IContentResult> GetStreamingChatMessageContentsWithFunctionsInternal(this IChatCompletionService chatCompletionService,
        Kernel kernel,
        ChatHistory chatHistory,
        PromptExecutionSettings settings,
        int iteration,
        [EnumeratorCancellation] CancellationToken token = default)
    {
        var responseStringBuilder = new StringBuilder();
        AuthorRole? authorRole = null;
        var fccBuilder = new FunctionCallContentBuilder();
        var isErorr = false;

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
                isErorr = true;
                yield return exceptionResult;
                break;
            }
        }

        var functionCalls = fccBuilder.Build();

        if (functionCalls.Any() && authorRole is not null) // todo should authorRole be always assistant ?
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
                await foreach (var functionExecutionReuslt in ExecuteFunctionCall(kernel, chatHistory, functionCall, token))
                {
                    yield return functionExecutionReuslt;
                }
            }
        }

        yield return new IterationResult
        {
            Iteration = iteration,
            IsStreamed = true,
            IsEmptyResponse = IsEmptyResponse(responseStringBuilder),
            IsError = isErorr,
            FunctionCalls = functionCalls.Select(fc => new FunctionCall { FunctionName = fc.FunctionName, Id = fc.Id, Arguments = fc.Arguments }).ToArray()
        };
    }

    private static async IAsyncEnumerable<IContentResult> GetChatMessageContentWithFunctionsInternal(this IChatCompletionService chatCompletionService,
        Kernel kernel,
        ChatHistory chatHistory,
        PromptExecutionSettings settings,
        int iteration,
        [EnumeratorCancellation] CancellationToken token = default)
    {
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
            if (syncFunctionCalls.Any())
            {
                chatHistory.Add(syncedCallResult);

                foreach (FunctionCallContent functionCall in syncFunctionCalls)
                {
                    yield return new FunctionCall { FunctionName = functionCall.FunctionName, Id = functionCall.Id, Arguments = functionCall.Arguments };
                    // user can remove function call from chat history
                    // user removed all function calls
                    if (!chatHistory.Contains(syncedCallResult)) break;
                    // user removed this function call
                    if (!syncedCallResult.Items.Contains(functionCall)) continue;
                    await foreach (var functionExecutionReuslt in ExecuteFunctionCall(kernel, chatHistory, functionCall, token))
                    {
                        yield return functionExecutionReuslt;
                    }
                }
            }

            yield return new IterationResult
            {
                Iteration = iteration,
                IsStreamed = false,
                IsEmptyResponse = string.IsNullOrWhiteSpace(syncedCallResult.Content?.ToString().Trim()),
                IsError = false,
                FunctionCalls = syncFunctionCalls.Select(fc => new FunctionCall { FunctionName = fc.FunctionName, Id = fc.Id, Arguments = fc.Arguments }).ToArray()
            };
        }
        if (result is CallingLLMExceptionResult callingLLMExceptionResult)
        {
            yield return callingLLMExceptionResult;
            yield return new IterationResult
            {
                Iteration = iteration,
                IsError = true,
                IsEmptyResponse = true,
                IsStreamed = false,
                FunctionCalls = []
            };
        }
    }

    private static async IAsyncEnumerable<IContentResult> ExecuteFunctionCall(Kernel kernel, ChatHistory chatHistory, FunctionCallContent functionCall, [EnumeratorCancellation] CancellationToken token)
    {
        FunctionResultContent functionResult;
        try
        {
            functionResult = await functionCall.InvokeAsync(kernel, token);
        }
        catch (Exception ex)
        {
            functionResult = new FunctionResultContent(functionCall, ex);
        }
        if (functionResult.Result is Exception exception)
        {
            chatHistory.Add(functionResult.ToChatMessage());
            yield return new FunctionExceptionResult { Id = functionResult.CallId, Exception = exception };
        }
        else if (functionResult.Result is IAsyncEnumerable<object?> functionCallResult)
        {
            var finalResult = new List<object?>();
            await foreach (var item in functionCallResult.WithCancellation(token))
            {
                finalResult.Add(item);
                yield return new StreamedFunctionExecutionResult
                {
                    Id = functionResult.CallId,
                    Result = item,
                    FunctionName = functionResult.FunctionName,
                    PluginName = functionResult.PluginName
                };
            }
            chatHistory.Add(new FunctionResultContent(functionCall, finalResult).ToChatMessage());
            yield return new FunctionExecutionResult
            {
                Id = functionResult.CallId,
                Result = finalResult,
                FunctionName = functionResult.FunctionName,
                PluginName = functionResult.PluginName
            };
        }
        else
        {
            chatHistory.Add(functionResult.ToChatMessage());
            yield return new FunctionExecutionResult
            {
                Id = functionResult.CallId,
                Result = functionResult.Result,
                FunctionName = functionResult.FunctionName,
                PluginName = functionResult.PluginName
            };
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
        return string.IsNullOrWhiteSpace(responseStringBuilder.ToString());
    }
}
