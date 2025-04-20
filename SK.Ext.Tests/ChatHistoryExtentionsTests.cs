using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SK.Ext;
using Xunit;

namespace sk.ext.tests;

public class ChatHistoryExtentionsTests
{
    [Fact]
    public void RemoveFunctionCall_RemovesFunctionCallAndResult()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        var functionCallContent = new FunctionCallContent("functionName", "pluginName", "callId");
        var functionResultContent = new FunctionResultContent(functionCallContent, "result");

        var chatMessageWithFunctionCall = new ChatMessageContent(AuthorRole.Assistant, new ChatMessageContentItemCollection { functionCallContent });
        var chatMessageWithFunctionResult = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent });

        chatHistory.Add(chatMessageWithFunctionCall);
        chatHistory.Add(chatMessageWithFunctionResult);

        // Act
        chatHistory.RemoveFunctionCall("callId");

        // Assert
        Assert.DoesNotContain(chatMessageWithFunctionCall, chatHistory);
        Assert.DoesNotContain(chatMessageWithFunctionResult, chatHistory);
    }

    [Fact]
    public void RemoveFunctionCall_DoesNothingIfCallIdNotFound()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        var functionCallContent = new FunctionCallContent("functionName", "pluginName", "callId");
        var chatMessageWithFunctionCall = new ChatMessageContent(AuthorRole.Assistant, new ChatMessageContentItemCollection { functionCallContent });

        chatHistory.Add(chatMessageWithFunctionCall);

        // Act
        chatHistory.RemoveFunctionCall("nonExistentCallId");

        // Assert
        Assert.Contains(chatMessageWithFunctionCall, chatHistory);
    }

    [Fact]
    public void ReplaceFunctionCallResult_UpdatesResult()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        var functionCallContent = new FunctionCallContent("functionName", "pluginName", "callId");
        var functionResultContent = new FunctionResultContent(functionCallContent, "oldResult");

        var chatMessageWithFunctionCall = new ChatMessageContent(AuthorRole.Assistant, new ChatMessageContentItemCollection { functionCallContent });
        var chatMessageWithFunctionResult = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent });

        chatHistory.Add(chatMessageWithFunctionCall);
        chatHistory.Add(chatMessageWithFunctionResult);

        // Act
        chatHistory.ReplaceFunctionCallResult("callId", "newResult");

        // Assert
        var updatedMessage = chatHistory.FirstOrDefault(m => m.Items.OfType<FunctionResultContent>().Any(x => x.CallId == "callId"));
        Assert.NotNull(updatedMessage);
        var updatedResult = (FunctionResultContent)updatedMessage.Items[0];
        Assert.Equal("newResult", updatedResult.Result);
    }

    [Fact]
    public void RemoveDuplicatedFunctionParallelCallResults_RemovesDuplicates()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        var functionCallContent1 = new FunctionCallContent("functionName", "pluginName", "callId1");
        var functionResultContent1 = new FunctionResultContent(functionCallContent1, "result1");

        var functionCallContent2 = new FunctionCallContent("functionName", "pluginName", "callId2");
        var functionResultContent2 = new FunctionResultContent(functionCallContent2, "result1");

        var chatMessageWithFunctionCall1 = new ChatMessageContent(AuthorRole.Assistant, new ChatMessageContentItemCollection { functionCallContent1, functionCallContent2 });
        var chatMessageWithFunctionResult1 = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent1 });
        var chatMessageWithFunctionResult2 = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent2 });

        chatHistory.Add(chatMessageWithFunctionCall1);
        chatHistory.Add(chatMessageWithFunctionResult1);
        chatHistory.Add(chatMessageWithFunctionResult2);

        // Act
        chatHistory.RemoveDuplicatedFunctionParallelCallResults().ToList();

        // Assert
        Assert.Single(chatHistory, m => m.Role == AuthorRole.Assistant);
        Assert.Single(chatHistory, m => m.Role == AuthorRole.Tool);
    }

    [Fact]
    public void RemoveDuplicatedFunctionParallelCallResults_RemovesDuplicatesWithParameters()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        var functionCallContent1 = new FunctionCallContent("functionName", "pluginName", "callId1", new KernelArguments { { "param1", "value1" } });
        var functionResultContent1 = new FunctionResultContent(functionCallContent1, "result1");

        var functionCallContent2 = new FunctionCallContent("functionName", "pluginName", "callId2", new KernelArguments { { "param1", "value1" } });
        var functionResultContent2 = new FunctionResultContent(functionCallContent2, "result1");

        var chatMessageWithFunctionCall1 = new ChatMessageContent(AuthorRole.Assistant, new ChatMessageContentItemCollection { functionCallContent1, functionCallContent2 });
        var chatMessageWithFunctionResult1 = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent1 });
        var chatMessageWithFunctionResult2 = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent2 });

        chatHistory.Add(chatMessageWithFunctionCall1);
        chatHistory.Add(chatMessageWithFunctionResult1);
        chatHistory.Add(chatMessageWithFunctionResult2);

        // Act
        chatHistory.RemoveDuplicatedFunctionParallelCallResults().ToList();

        // Assert
        Assert.Single(chatHistory, m => m.Role == AuthorRole.Assistant);
        Assert.Single(chatHistory, m => m.Role == AuthorRole.Tool);
    }

    [Fact]
    public void RemoveDuplicatedFunctionParallelCallResults_JoinsFunctionParameters()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        var functionCallContent1 = new FunctionCallContent("functionName", "pluginName", "callId1", new KernelArguments { { "param1", "value1" } });
        var functionResultContent1 = new FunctionResultContent(functionCallContent1, "result1");

        var functionCallContent2 = new FunctionCallContent("functionName", "pluginName", "callId2", new KernelArguments { { "param1", "value2" } });
        var functionResultContent2 = new FunctionResultContent(functionCallContent2, "result1");

        var chatMessageWithFunctionCall1 = new ChatMessageContent(AuthorRole.Assistant, new ChatMessageContentItemCollection { functionCallContent1, functionCallContent2 });
        var chatMessageWithFunctionResult1 = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent1 });
        var chatMessageWithFunctionResult2 = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent2 });

        chatHistory.Add(chatMessageWithFunctionCall1);
        chatHistory.Add(chatMessageWithFunctionResult1);
        chatHistory.Add(chatMessageWithFunctionResult2);

        // Act
        chatHistory.RemoveDuplicatedFunctionParallelCallResults().ToList();

        // Assert
        var updatedMessage = chatHistory.FirstOrDefault(m => m.Role == AuthorRole.Assistant);
        Assert.NotNull(updatedMessage);
        var combinedFunctionCall = updatedMessage.Items.OfType<FunctionCallContent>().FirstOrDefault();
        Assert.NotNull(combinedFunctionCall);
        Assert.True(combinedFunctionCall.Arguments.ContainsName("param1"));
        Assert.Equal("value1;value2", combinedFunctionCall.Arguments["param1"]);
    }

    [Fact]
    public void SimulateFunctionCalls_AddsFunctionCallsAndResults()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        var simulatedFunctionCalls = new List<SimulatedFunctionCall>
        {
            new SimulatedFunctionCall
            {
                FunctionName = "functionName1",
                PluginName = "pluginName1",
                Arguments = new Dictionary<string, object?> { { "param1", "value1" } },
                Result = "result1"
            },
            new SimulatedFunctionCall
            {
                FunctionName = "functionName2",
                PluginName = "pluginName2",
                Arguments = new Dictionary<string, object?> { { "param2", "value2" } },
                Result = "result2"
            }
        };

        // Act
        chatHistory.SimulateFunctionCalls(simulatedFunctionCalls);

        // Assert
        var assistantMessage = chatHistory.FirstOrDefault(m => m.Role == AuthorRole.Assistant);
        Assert.NotNull(assistantMessage);
        Assert.Equal(2, assistantMessage.Items.Count);
        Assert.All(assistantMessage.Items, item => Assert.IsType<FunctionCallContent>(item));

        var toolMessages = chatHistory.Where(m => m.Role == AuthorRole.Tool).ToList();
        Assert.Equal(2, toolMessages.Count);
        Assert.All(toolMessages, message => Assert.Single(message.Items));
        Assert.All(toolMessages, message => Assert.IsType<FunctionResultContent>(message.Items[0]));
    }

    [Fact]
    public void SimulateFunctionCalls_EmptyFunctionCalls()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        var simulatedFunctionCalls = new List<SimulatedFunctionCall>();

        // Act
        chatHistory.SimulateFunctionCalls(simulatedFunctionCalls);

        // Assert
        Assert.Empty(chatHistory);
    }

    [Fact]
    public void RemoveDuplicatedFunctionCallResults_RemoveDuplicatedFunctionCalls()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        var functionCallContent1 = new FunctionCallContent("functionName", "pluginName", "callId1", new KernelArguments { { "param1", "value1" } });
        var functionResultContent1 = new FunctionResultContent(functionCallContent1, "result1");

        var functionCallContent2 = new FunctionCallContent("functionName", "pluginName", "callId2", new KernelArguments { { "param1", "value2" } });
        var functionResultContent2 = new FunctionResultContent(functionCallContent2, "result1");

        var chatMessageWithFunctionCall1 = new ChatMessageContent(AuthorRole.Assistant, new ChatMessageContentItemCollection { functionCallContent1 });
        var chatMessageWithFunctionResult1 = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent1 });
        var chatMessageWithFunctionCall2 = new ChatMessageContent(AuthorRole.Assistant, new ChatMessageContentItemCollection { functionCallContent2 });
        var chatMessageWithFunctionResult2 = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent2 });

        chatHistory.Add(chatMessageWithFunctionCall1);
        chatHistory.Add(chatMessageWithFunctionResult1);
        chatHistory.Add(chatMessageWithFunctionCall2);
        chatHistory.Add(chatMessageWithFunctionResult2);

        // Act
        chatHistory.RemoveDuplicatedFunctionCallResults().ToList();

        // Assert
        var updatedMessage = chatHistory.FirstOrDefault(m => m.Role == AuthorRole.Assistant);
        Assert.NotNull(updatedMessage);
        var combinedFunctionCall = updatedMessage.Items.OfType<FunctionCallContent>().FirstOrDefault();
        Assert.NotNull(combinedFunctionCall);
        Assert.True(combinedFunctionCall.Arguments.ContainsName("param1"));
        Assert.Equal("value1;value2", combinedFunctionCall.Arguments["param1"]);

        var toolMessages = chatHistory.Where(m => m.Role == AuthorRole.Tool).ToList();
        Assert.Single(toolMessages);
        Assert.Single(toolMessages[0].Items);
        var combinedFunctionResult = toolMessages[0].Items.OfType<FunctionResultContent>().FirstOrDefault();
        Assert.NotNull(combinedFunctionResult);
        Assert.Equal("result1", combinedFunctionResult.Result);
    }

    [Fact]
    public void RemoveDuplicatedFunctionCallResults_RemoveMoreThanOneDuplicatedCall()
    {
        // Arrange
        var chatHistory = new ChatHistory();
        var functionCallContent1 = new FunctionCallContent("functionName", "pluginName", "callId1", new KernelArguments { { "param1", "value1" } });
        var functionResultContent1 = new FunctionResultContent(functionCallContent1, "result1");

        var functionCallContent2 = new FunctionCallContent("functionName", "pluginName", "callId2", new KernelArguments { { "param1", "value2" } });
        var functionResultContent2 = new FunctionResultContent(functionCallContent2, "result1");

        var functionCallContent3 = new FunctionCallContent("functionName", "pluginName", "callId3", new KernelArguments { { "param2", "value3" } });
        var functionResultContent3 = new FunctionResultContent(functionCallContent3, "result2");

        var functionCallContent4 = new FunctionCallContent("functionName", "pluginName", "callId4", new KernelArguments { { "param2", "value4" } });
        var functionResultContent4 = new FunctionResultContent(functionCallContent4, "result2");

        var chatMessageWithFunctionCall1 = new ChatMessageContent(AuthorRole.Assistant, new ChatMessageContentItemCollection { functionCallContent1, functionCallContent3 });
        var chatMessageWithFunctionResult1 = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent1 });
        var chatMessageWithFunctionResult3 = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent3 });
        var chatMessageWithFunctionCall2 = new ChatMessageContent(AuthorRole.Assistant, new ChatMessageContentItemCollection { functionCallContent2, functionCallContent4 });
        var chatMessageWithFunctionResult2 = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent2 });
        var chatMessageWithFunctionResult4 = new ChatMessageContent(AuthorRole.Tool, new ChatMessageContentItemCollection { functionResultContent4 });

        chatHistory.Add(chatMessageWithFunctionCall1);
        chatHistory.Add(chatMessageWithFunctionResult1);
        chatHistory.Add(chatMessageWithFunctionResult3);
        chatHistory.Add(chatMessageWithFunctionCall2);
        chatHistory.Add(chatMessageWithFunctionResult2);
        chatHistory.Add(chatMessageWithFunctionResult4);

        // Act
        chatHistory.RemoveDuplicatedFunctionCallResults().ToList();

        // Assert
        var updatedMessage = chatHistory.Where(m => m.Role == AuthorRole.Assistant).ToList();
        Assert.Equal(2, updatedMessage.Count);

        var combinedFunctionCalls1 = updatedMessage[0].Items.OfType<FunctionCallContent>().ToList();
        Assert.True(combinedFunctionCalls1[0].Arguments.ContainsName("param1"));
        Assert.Equal("value1;value2", combinedFunctionCalls1[0].Arguments["param1"]);

        var combinedFunctionCalls2 = updatedMessage[1].Items.OfType<FunctionCallContent>().ToList();
        Assert.True(combinedFunctionCalls2[0].Arguments.ContainsName("param2"));
        Assert.Equal("value3;value4", combinedFunctionCalls2[0].Arguments["param2"]);

        var toolMessages = chatHistory.Where(m => m.Role == AuthorRole.Tool).ToList();
        Assert.Equal(2, toolMessages.Count);

        Assert.Single(toolMessages[0].Items);
        var combinedFunctionResult1 = toolMessages[0].Items.OfType<FunctionResultContent>().FirstOrDefault();
        Assert.NotNull(combinedFunctionResult1);
        Assert.Equal("result1", combinedFunctionResult1.Result);

        Assert.Single(toolMessages[1].Items);
        var combinedFunctionResult2 = toolMessages[1].Items.OfType<FunctionResultContent>().FirstOrDefault();
        Assert.NotNull(combinedFunctionResult2);
        Assert.Equal("result2", combinedFunctionResult2.Result);
    }
}
