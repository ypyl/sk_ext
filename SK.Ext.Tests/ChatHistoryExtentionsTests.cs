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
    public void RemoveDuplicatedFunctionCallResults_RemovesDuplicates()
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
        chatHistory.RemoveDuplicatedFunctionCallResults();

        // Assert
        Assert.Single(chatHistory, m => m.Role == AuthorRole.Assistant);
        Assert.Single(chatHistory, m => m.Role == AuthorRole.Tool);
    }

    [Fact]
    public void RemoveDuplicatedFunctionCallResults_RemovesDuplicatesWithParameters()
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
        chatHistory.RemoveDuplicatedFunctionCallResults();

        // Assert
        Assert.Single(chatHistory, m => m.Role == AuthorRole.Assistant);
        Assert.Single(chatHistory, m => m.Role == AuthorRole.Tool);
    }

    [Fact]
    public void RemoveDuplicatedFunctionCallResults_JoinsFunctionParameters()
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
        chatHistory.RemoveDuplicatedFunctionCallResults();

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
}
