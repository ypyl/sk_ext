using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SK.Ext;

public readonly struct SimulatedFunctionCall
{
    public required string FunctionName { get; init; }
    public required string PluginName { get; init; }
    public required IDictionary<string, object?> Arguments { get; init; }
    public required object? Result { get; init; }
}

public static class ChatHistoryExtentions
{
    public static void RemoveFunctionCall(this ChatHistory chatHistory, string? callId)
    {
        var chatMessage = chatHistory.FirstOrDefault(m => m.Items.Any(x => x is FunctionCallContent fcc && fcc.Id == callId));
        if (chatMessage is null)
        {
            return;
        }
        if (chatMessage.Items.Count == 1)
        {
            chatHistory.Remove(chatMessage);
        }
        else
        {
            var functionCallContext = chatMessage.Items.OfType<FunctionCallContent>().First(x => x.Id == callId);
            chatMessage.Items.Remove(functionCallContext);
        }
        var chatMessageWithResult = chatHistory.FirstOrDefault(m => m.Items.OfType<FunctionResultContent>().Any(x => x.CallId == callId));
        if (chatMessageWithResult is null)
        {
            return;
        }
        chatHistory.Remove(chatMessageWithResult);
    }

    public static void ReplaceFunctionCallResult(this ChatHistory chatHistory, string? callId, object? result)
    {
        var chatMessage = chatHistory.FirstOrDefault(m => m.Role == AuthorRole.Tool && m.Items.Count == 1 && m.Items[0] is FunctionResultContent frc && frc.CallId == callId);
        if (chatMessage is null)
        {
            return;
        }
        var functionResultContent = (FunctionResultContent)chatMessage.Items[0];
        chatMessage.Items[0] = new FunctionResultContent(functionResultContent.FunctionName, functionResultContent.PluginName, functionResultContent.CallId, result);
    }

    public static void SimulateFunctionCalls(this ChatHistory chatHistory, IEnumerable<SimulatedFunctionCall> functionCallToSimulates)
    {
        if (!functionCallToSimulates.Any())
        {
            return;
        }
        var assistantItems = new ChatMessageContentItemCollection();
        var toolMessages = new List<ChatMessageContent>();

        foreach (var functionCall in functionCallToSimulates)
        {
            var id = Guid.NewGuid().ToString();
            // Add function call content to assistant items
            assistantItems.Add(
                new FunctionCallContent(
                    functionName: functionCall.FunctionName,
                    pluginName: functionCall.PluginName,
                    id: id,
                    arguments: new KernelArguments(functionCall.Arguments)
                )
            );

            // Add a simulated function result from the tool role
            toolMessages.Add(
                new ChatMessageContent
                {
                    Role = AuthorRole.Tool,
                    Items = [
                        new FunctionResultContent(
                            functionName: functionCall.FunctionName,
                            pluginName: functionCall.PluginName,
                            callId: id,
                            result: functionCall.Result
                        )
                    ]
                }
            );
        }

        // Add a single chat message with all assistant items
        chatHistory.Add(
            new ChatMessageContent
            {
                Role = AuthorRole.Assistant,
                Items = assistantItems
            }
        );

        // Add all tool messages to chat history
        foreach (var toolMessage in toolMessages)
        {
            chatHistory.Add(toolMessage);
        }
    }

    /// <summary>
    /// Removes duplicated function call results from the chat history per assistant message.
    /// This method identifies function calls that have the same result and merges them into a single function call with a combined result.
    /// But it does not find duplications across different assistant messages (only within the same message).
    /// Use <see cref="RemoveDuplicatedFunctionCallResults"/> for removing duplication withing the whole history.
    /// </summary>
    /// <param name="chatHistory"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static IEnumerable<List<string>> RemoveDuplicatedFunctionParallelCallResults(this ChatHistory chatHistory)
    {
        var assistantMessagesWithFunctionCall = chatHistory.Where(m => m.Role == AuthorRole.Assistant && m.Items.Count > 0 && m.Items.OfType<FunctionCallContent>().Any()).ToList();
        for (int i = assistantMessagesWithFunctionCall.Count - 1; i >= 0; i--)
        {
            var assistantMessage = assistantMessagesWithFunctionCall[i];
            var functionCallGroups = assistantMessage.Items.OfType<FunctionCallContent>().GroupBy(x => x.FunctionName + x.PluginName);
            var mergeCallIds = new List<string>();

            foreach (var group in functionCallGroups)
            {
                var functionCallIds = group.Select(x => x.Id).ToList();
                var messagesWithFunctionResults = chatHistory.Where(m => m.Role == AuthorRole.Tool && m.Items.Count == 1 && m.Items[0] is FunctionResultContent frc
                    && functionCallIds.Contains(frc.CallId));
                var groupedResults = messagesWithFunctionResults
                    .GroupBy(x =>
                    {
                        var result = ((FunctionResultContent)x.Items[0]).Result;
                        return result is IComparable comparableResult ? comparableResult : null;
                    }, new ResultComparer())
                    .Where(x => x.Key != null && x.Count() > 1);
                foreach (var groupResult in groupedResults)
                {
                    mergeCallIds.AddRange(groupResult.Select(x => ((FunctionResultContent)x.Items[0]).CallId).Where(x => x != null)!);
                }
            }

            if (mergeCallIds.Count == 0)
            {
                continue;
            }

            yield return mergeCallIds;

            var functionCallContextsToJoins = assistantMessage.Items.OfType<FunctionCallContent>()
                .Where(x => x.Id != null && mergeCallIds.Contains(x.Id))
                .ToList();

            if (functionCallContextsToJoins.Count != mergeCallIds.Count)
            {
                // Ensure all mergeCallIds exist in assistantMessage.Items
                throw new InvalidOperationException("Mismatch between mergeCallIds and available FunctionCallContent items.");
            }
            var newFunctionCallContext = CombineFunctionCallContents(functionCallContextsToJoins);
            foreach (var functionCallContextToDelete in functionCallContextsToJoins)
            {
                assistantMessage.Items.Remove(functionCallContextToDelete);
            }
            assistantMessage.Items.Add(newFunctionCallContext);

            var functionResults = chatHistory.Where(m => m.Role == AuthorRole.Tool && m.Items.Count == 1 && m.Items[0] is FunctionResultContent frc
                && frc.CallId != null && mergeCallIds.Contains(frc.CallId)).ToList();
            var newFunctionResult = CombineFunctionResults(functionResults.Select(x => (FunctionResultContent)x.Items[0]), newFunctionCallContext.Id!);
            for (int y = functionResults.Count - 1; y >= 0; y--)
            {
                var functionResultToDelete = functionResults[y];
                chatHistory.Remove(functionResultToDelete);
            }
            var indexToInsert = chatHistory.IndexOf(assistantMessage) + 1;
            chatHistory.Insert(indexToInsert, newFunctionResult.ToChatMessage());
        }
    }

    /// <summary>
    /// Removes duplicated function call results from the chat history.
    /// This method identifies function calls that have the same result and merges them into a single function call with a combined result.
    /// Add the new merged function call and result to the chat history at the end.
    /// Check <see cref="RemoveDuplicatedFunctionParallelCallResults"/> for removing duplication within the same assistant message.
    /// </summary>
    /// <param name="chatHistory"></param>
    /// <returns></returns>
    public static IEnumerable<List<string>> RemoveDuplicatedFunctionCallResults(this ChatHistory chatHistory)
    {
        var assistantMessagesWithFunctionCall = chatHistory.Where(m => m.Role == AuthorRole.Assistant && m.Items.Count > 0 && m.Items.OfType<FunctionCallContent>().Any()).ToList();
        var functionCallGroups = assistantMessagesWithFunctionCall.SelectMany(x => x.Items).OfType<FunctionCallContent>()
            .GroupBy(x => x.FunctionName + x.PluginName);
        var mergeCallIdGroups = new List<List<string>>();

        // Iterate through each group of function calls
        // and find function call ids that have the same result
        // and are not null or empty
        // so we have a group of such ids to merge

        foreach (var group in functionCallGroups)
        {
            var functionCallIds = group.Select(x => x.Id).ToList();
            var messagesWithFunctionResults = chatHistory.Where(m => m.Role == AuthorRole.Tool && m.Items.Count == 1 && m.Items[0] is FunctionResultContent frc
                && functionCallIds.Contains(frc.CallId));
            var groupedResults = messagesWithFunctionResults
                .GroupBy(x =>
                {
                    var result = ((FunctionResultContent)x.Items[0]).Result;
                    return result is IComparable comparableResult ? comparableResult : null;
                }, new ResultComparer())
                .Where(x => x.Key != null && x.Count() > 1);
            foreach (var groupResult in groupedResults)
            {
                mergeCallIdGroups.Add(groupResult.Select(x => ((FunctionResultContent)x.Items[0]).CallId!).Where(x => x != null).ToList());
            }
        }

        if (mergeCallIdGroups.Count == 0)
        {
            yield break;
        }

        // iterate via each group of function call ids to merge
        // and remove the duplicates from the chat history
        // and add the new merged function call and result to the chat history

        // TODO should we have only one message with all function calls (one new assistant message)?
        // currently we have creating asistant/tool message for each group of function call ids
        foreach (var mergeCallIds in mergeCallIdGroups)
        {
            if (mergeCallIds.Count == 0)
            {
                continue;
            }
            yield return mergeCallIds;
            var functionCallContextsToJoins = assistantMessagesWithFunctionCall
                .ToLookup(
                    x => x,
                    y => y.Items.OfType<FunctionCallContent>().Where(x => x.Id != null && mergeCallIds.Contains(x.Id)).ToList())
                .Where(x => x.Any()).ToList();

            var fnctionCallContexts = new List<FunctionCallContent>();
            foreach (var functionCallContextToDelete in functionCallContextsToJoins)
            {
                var functionCallsToRemove = functionCallContextToDelete.SelectMany(x => x).ToList();
                foreach (var functionCallToDelete in functionCallsToRemove)
                {
                    fnctionCallContexts.Add(functionCallToDelete);
                    functionCallContextToDelete.Key.Items.Remove(functionCallToDelete);
                }
                if (functionCallContextToDelete.Key.Items.Count == 0)
                {
                    chatHistory.Remove(functionCallContextToDelete.Key);
                }
            }
            var newFunctionCallContext = CombineFunctionCallContents(fnctionCallContexts);
            chatHistory.Add(new ChatMessageContent(AuthorRole.Assistant, [newFunctionCallContext]));

            var functionResults = chatHistory.Where(m => m.Role == AuthorRole.Tool && m.Items.Count == 1 && m.Items[0] is FunctionResultContent frc
                && frc.CallId != null && mergeCallIds.Contains(frc.CallId)).ToList();
            var newFunctionResult = CombineFunctionResults(functionResults.Select(x => (FunctionResultContent)x.Items[0]), newFunctionCallContext.Id!);
            for (int y = functionResults.Count - 1; y >= 0; y--)
            {
                var functionResultToDelete = functionResults[y];
                chatHistory.Remove(functionResultToDelete);
            }
            chatHistory.Add(newFunctionResult.ToChatMessage());
        }
    }

    private static FunctionCallContent CombineFunctionCallContents(IEnumerable<FunctionCallContent> functionCallContents)
    {
        var functionName = functionCallContents.First().FunctionName;
        var pluginName = functionCallContents.First().PluginName;
        var id = functionCallContents.Select(x => x.Id).FirstOrDefault(x => x is not null);
        return new FunctionCallContent(functionName, pluginName, id, JoinKernelArguments(functionCallContents.Select(x => x.Arguments)));
    }

    private static FunctionResultContent CombineFunctionResults(IEnumerable<FunctionResultContent> functionResultContents, string callId)
    {
        var functionName = functionResultContents.First().FunctionName;
        var pluginName = functionResultContents.First().PluginName;
        var result = functionResultContents.Select(x => x.Result).FirstOrDefault(x => x is not null);
        return new FunctionResultContent(functionName, pluginName, callId, result);
    }

    private static KernelArguments JoinKernelArguments(IEnumerable<KernelArguments?> keyValuePairs)
    {
        var result = new KernelArguments();
        foreach (var ka in keyValuePairs.Where(x => x is not null))
        {
            if (ka is null) continue;
            foreach (var keyValuePair in ka)
            {
                if (keyValuePair.Value is null) continue;
                if (result.ContainsName(keyValuePair.Key))
                {
                    var existingValue = result[keyValuePair.Key]?.ToString();
                    var newValue = keyValuePair.Value?.ToString();
                    if (existingValue != null && newValue != null && existingValue.Contains(newValue))
                    {
                        continue;
                    }
                    result[keyValuePair.Key] = string.Join(";", [result[keyValuePair.Key], keyValuePair.Value]);
                }
                else
                {
                    result.Add(keyValuePair.Key, keyValuePair.Value);
                }
            }
        }
        return result;
    }

    private class ResultComparer : IEqualityComparer<IComparable?>
    {
        public bool Equals(IComparable? x, IComparable? y)
        {
            return x?.CompareTo(y) == 0;
        }

        public int GetHashCode(IComparable? obj)
        {
            return obj?.GetHashCode() ?? 0;
        }
    }
}
