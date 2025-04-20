using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SK.Ext;

public readonly struct SimulatedFunctionCall
{
    public required string FunctionName { get; init; }
    public required string PluginName { get; init; }
    public required IDictionary<string, object?> Arguments { get; init; }
    public required object Result { get; init; }
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
        if (functionCallToSimulates.Count() == 0)
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

    // public static IEnumerable<List<string>> RemoveDuplicatedFunctionCallResults(this ChatHistory chatHistory)
    // {
    //     var assistantMessagesWithFunctionCall = chatHistory.Where(m => m.Role == AuthorRole.Assistant && m.Items.Count > 0 && m.Items.OfType<FunctionCallContent>().Any()).ToList();
    //     var assistantMessage = assistantMessagesWithFunctionCall;
    //     var functionCallGroups = assistantMessagesWithFunctionCall.SelectMany(x => x.Items).OfType<FunctionCallContent>().GroupBy(x => x.FunctionName + x.PluginName);
    //     var mergeCallIds = new List<string>();

    //     foreach (var group in functionCallGroups)
    //     {
    //         var functionCallIds = group.Select(x => x.Id).ToList();
    //         var messagesWithFunctionResults = chatHistory.Where(m => m.Role == AuthorRole.Tool && m.Items.Count == 1 && m.Items[0] is FunctionResultContent frc
    //             && functionCallIds.Contains(frc.CallId));
    //         var groupedResults = messagesWithFunctionResults
    //             .GroupBy(x =>
    //             {
    //                 var result = ((FunctionResultContent)x.Items[0]).Result;
    //                 return result is IComparable comparableResult ? comparableResult : null;
    //             }, new ResultComparer())
    //             .Where(x => x.Key != null && x.Count() > 1);
    //         foreach (var groupResult in groupedResults)
    //         {
    //             mergeCallIds.AddRange(groupResult.Select(x => ((FunctionResultContent)x.Items[0]).CallId).Where(x => x != null)!);
    //         }
    //     }

    //     if (mergeCallIds.Count == 0)
    //     {
    //         yield break;
    //     }

    //     yield return mergeCallIds;

        // var functionCallContextsToJoins = assistantMessagesWithFunctionCall.SelectMany(x => x.Items).OfType<FunctionCallContent>()
        //     .Where(x => x.Id != null && mergeCallIds.Contains(x.Id))
        //     .ToList();

        // if (functionCallContextsToJoins.Count != mergeCallIds.Count)
        // {
        //     // Ensure all mergeCallIds exist in assistantMessage.Items
        //     throw new InvalidOperationException("Mismatch between mergeCallIds and available FunctionCallContent items.");
        // }
        // var newFunctionCallContext = CombineFunctionCallContents(functionCallContextsToJoins);
        // foreach (var functionCallContextToDelete in functionCallContextsToJoins)
        // {
        //     assistantMessage.Items.Remove(functionCallContextToDelete);
        // }
        // assistantMessage.Items.Add(newFunctionCallContext);

        // var functionResults = chatHistory.Where(m => m.Role == AuthorRole.Tool && m.Items.Count == 1 && m.Items[0] is FunctionResultContent frc
        //     && frc.CallId != null && mergeCallIds.Contains(frc.CallId)).ToList();
        // var newFunctionResult = CombineFunctionResults(functionResults.Select(x => (FunctionResultContent)x.Items[0]), newFunctionCallContext.Id!);
        // for (int y = functionResults.Count - 1; y >= 0; y--)
        // {
        //     var functionResultToDelete = functionResults[y];
        //     chatHistory.Remove(functionResultToDelete);
        // }
        // var indexToInsert = chatHistory.IndexOf(assistantMessage) + 1;
        // chatHistory.Insert(indexToInsert, newFunctionResult.ToChatMessage());
    // }

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
