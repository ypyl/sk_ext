using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;
using SK.Ext.Models.Result;

namespace SK.Ext;

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
                    isContinue = iterationResult.CalledFullFunctions.Length > 0;
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
                    isContinue = iterationResult.CalledFullFunctions.Length > 0;
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
                    isContinue = iterationResult.CalledFullFunctions.Length > 0;
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
                    yield return new TextResult
                    {
                        Text = streamingChatMessageContent.Result.Content,
                        CompletionId = MetadataInfo(streamingChatMessageContent.Result.Metadata, "CompletionId"),
                        CreatedAt = ParseWithDefault(streamingChatMessageContent.Result.Metadata?["CreatedAt"]?.ToString()),
                        SystemFingerprint = MetadataInfo(streamingChatMessageContent.Result.Metadata, "SystemFingerprint"),
                        Model = streamingChatMessageContent.Result.ModelId,
                        IsStreamed = true
                    };
                }
                authorRole ??= streamingChatMessageContent.Result.Role;
                fccBuilder.Append(streamingChatMessageContent.Result);

                if (streamingChatMessageContent.Result.Metadata is not null)
                {
                    var metadata = streamingChatMessageContent.Result.Metadata;

                    if (metadata.TryGetValue("FinishReason", out var finishReasonObject) && finishReasonObject is not null)
                    {
                        yield return new FinishReasonResult
                        {
                            FinishReason = finishReasonObject,
                            CompletionId = MetadataInfo(streamingChatMessageContent.Result.Metadata, "CompletionId"),
                            CreatedAt = ParseWithDefault(streamingChatMessageContent.Result.Metadata?["CreatedAt"]?.ToString()),
                            SystemFingerprint = MetadataInfo(streamingChatMessageContent.Result.Metadata, "SystemFingerprint"),
                            Model = streamingChatMessageContent.Result.ModelId,
                            IsStreamed = true
                        };
                    }

                    var outputTokens = GetOutputTokensFromMetadata(metadata);
                    if (outputTokens is not null)
                    {
                        yield return new UsageResult
                        {
                            IsStreamed = true,
                            OutputTokenCount = outputTokens.OutputTokenCount,
                            InputTokenCount = outputTokens.InputTokenCount,
                            TotalTokenCount = outputTokens.TotalTokenCount,
                            CompletionId = MetadataInfo(streamingChatMessageContent.Result.Metadata, "CompletionId"),
                            CreatedAt = ParseWithDefault(streamingChatMessageContent.Result.Metadata?["CreatedAt"]?.ToString()),
                            SystemFingerprint = MetadataInfo(streamingChatMessageContent.Result.Metadata, "SystemFingerprint"),
                            Model = streamingChatMessageContent.Result.ModelId,
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
                yield return new FunctionCall
                {
                    Name = functionCall.FunctionName,
                    PluginName = functionCall.PluginName,
                    Id = functionCall.Id,
                    Arguments = functionCall.Arguments,
                    IsStreamed = true
                };
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
            CalledFullFunctions = functionCalls.Select(fc => new CalledFunction
            {
                FunctionName = fc.FunctionName,
                Id = fc.Id,
                PluginName = fc.PluginName
            }).ToArray()
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
                yield return new TextResult
                {
                    Text = syncedCallResult.Content,
                    CompletionId = MetadataInfo(callingLLMResult.Result.Metadata, "CompletionId"),
                    CreatedAt = ParseWithDefault(callingLLMResult.Result.Metadata?["CreatedAt"]?.ToString()),
                    SystemFingerprint = MetadataInfo(callingLLMResult.Result.Metadata, "SystemFingerprint"),
                    Model = callingLLMResult.Result.ModelId,
                    IsStreamed = false
                };
            }
            if (syncedCallResult.Metadata is not null)
            {
                if (syncedCallResult.Metadata.TryGetValue("FinishReason", out var finishReasonObject) && finishReasonObject is not null)
                {
                    yield return new FinishReasonResult
                    {
                        FinishReason = finishReasonObject,
                        CompletionId = MetadataInfo(syncedCallResult.Metadata, "CompletionId"),
                        CreatedAt = ParseWithDefault(syncedCallResult.Metadata?["CreatedAt"]?.ToString()),
                        SystemFingerprint = MetadataInfo(syncedCallResult.Metadata, "SystemFingerprint"),
                        Model = syncedCallResult.ModelId,
                        IsStreamed = false
                    };
                }
                var outputTokens = GetOutputTokensFromMetadata(syncedCallResult.Metadata);
                if (outputTokens is not null)
                {
                    yield return new UsageResult
                    {
                        IsStreamed = false,
                        OutputTokenCount = outputTokens.OutputTokenCount,
                        InputTokenCount = outputTokens.InputTokenCount,
                        TotalTokenCount = outputTokens.TotalTokenCount,
                        CompletionId = MetadataInfo(callingLLMResult.Result.Metadata, "CompletionId"),
                        CreatedAt = ParseWithDefault(callingLLMResult.Result.Metadata?["CreatedAt"]?.ToString()),
                        SystemFingerprint = MetadataInfo(callingLLMResult.Result.Metadata, "SystemFingerprint"),
                        Model = callingLLMResult.Result.ModelId,
                    };
                }
            }

            var syncFunctionCalls = FunctionCallContent.GetFunctionCalls(syncedCallResult);
            if (syncFunctionCalls.Any())
            {
                chatHistory.Add(syncedCallResult);

                foreach (FunctionCallContent functionCall in syncFunctionCalls)
                {
                    yield return new FunctionCall
                    {
                        Name = functionCall.FunctionName,
                        PluginName = functionCall.PluginName,
                        Id = functionCall.Id,
                        Arguments = functionCall.Arguments,
                        IsStreamed = false
                    };
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
                CalledFullFunctions = syncFunctionCalls.Select(fc => new CalledFunction
                {
                    FunctionName = fc.FunctionName,
                    Id = fc.Id,
                    PluginName = fc.PluginName
                }).ToArray()
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
                CalledFullFunctions = []
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
            yield return new FunctionExceptionResult
            {
                Id = functionResult.CallId,
                Exception = exception,
                FunctionName = functionResult.FunctionName,
                PluginName = functionResult.PluginName
            };
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
                Name = functionResult.FunctionName,
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
                Name = functionResult.FunctionName,
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

    private static DateTime? ParseWithDefault(string? dateTimeString, DateTime? defaultValue = null)
    {
        if (string.IsNullOrWhiteSpace(dateTimeString))
        {
            return defaultValue;
        }

        if (DateTime.TryParse(dateTimeString, out var dateTime))
        {
            return dateTime;
        }

        return defaultValue;
    }

    private static string? MetadataInfo(IReadOnlyDictionary<string, object?>? metadata, string key)
    {
        if (metadata is not null && metadata.TryGetValue(key, out var value))
        {
            return value?.ToString();
        }

        return null;
    }
}
