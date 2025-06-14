# SK.Ext

`SK.Ext` is a .NET library that extends the functionality of the Microsoft Semantic Kernel. It provides additional utilities and extensions to enhance the development experience when working with Semantic Kernel.

## Features

- **Chat Completion Extensions**: Stream chat messages with functions and advanced result handling.
- **Chat History Extensions**: Manage and manipulate chat history efficiently.
- **Async Enumerable Extensions**: Utilities for working with asynchronous streams.
- **Completion Runtime**: Abstraction for running completions with custom logic.
- **Result Types**: Rich result types for LLM calls, including streaming and error handling.

## Installation

To install `SK.Ext`, add the following package reference to your project file:

```xml
<PackageReference Include="SK.Ext" Version="1.0.7" />
```

Alternatively, you can install it via the .NET CLI:

```sh
dotnet add package SK.Ext --version 1.0.7
```

## Main Extension Classes

- `ChatCompletionServiceExtentions`: Extension methods for streaming chat completions with function support.
- `ChatHistoryExtentions`: Methods for manipulating chat history, function calls, and results.
- `AsyncEnumerableExtentions`: Utilities for merging and working with async enumerables.
- `CompletionRuntime` / `ICompletionRuntime`: Abstractions for running completions with custom logic and streaming results.
- `CallingLLMResult`, `CallingLLMStreamedResult`: Result types for LLM calls.

## Usage

### Chat Completion Extensions

The `ChatCompletionServiceExtentions` class provides methods to stream chat messages with functions. Here is an example:

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SK.Ext;

var kernel = Kernel.CreateBuilder().Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
var chatHistory = new ChatHistory();
var settings = new PromptExecutionSettings();

await foreach (var result in chatCompletionService.StreamChatMessagesWithFunctions(kernel, chatHistory, settings))
{
    switch (result)
    {
        case TextResult textResult:
            Console.Write(textResult.Text);
            break;
        case FunctionCall functionCall:
            Console.WriteLine($"\nCalling function: {functionCall.FunctionName}");
            break;
        case FunctionExecutionResult functionResult:
            Console.WriteLine($"Function result: {functionResult.Result}");
            break;
        case StreamedFunctionExecutionResult streamedResult:
            Console.WriteLine($"Streaming result: {streamedResult.Result}");
            break;
        case FunctionExceptionResult exceptionResult:
            Console.WriteLine($"Function error: {exceptionResult.Exception.Message}");
            break;
        case UsageResult usageResult:
            Console.WriteLine($"Tokens used: {usageResult.TotalTokenCount}");
            break;
        case CallingLLM callingLLM:
            Console.WriteLine("Calling LLM...");
            break;
        case CallingLLMResult llmResult:
            Console.WriteLine($"LLM result: {llmResult.Result}");
            break;
        case CallingLLMStreamedResult streamedLlmResult:
            Console.WriteLine($"LLM streamed result: {streamedLlmResult.Result}");
            break;
    }
}
```

### Chat History Extensions

The `ChatHistoryExtentions` class provides methods to manage and manipulate chat history. Here is an example:

```csharp
using Microsoft.SemanticKernel.ChatCompletion;
using SK.Ext;

var chatHistory = new ChatHistory();
chatHistory.RemoveFunctionCall("callId");
chatHistory.ReplaceFunctionCallResult("callId", new { Result = "result" });
chatHistory.RemoveDuplicatedFunctionCallResults();
```

### Completion Runtime

You can use the `CompletionRuntime` class to run completions with custom logic and streaming support:

```csharp
using SK.Ext;
using SK.Ext.Models;

ICompletionRuntime runtime = new CompletionRuntime(chatCompletionService);
var context = new CompletionContext();
await foreach (var result in runtime.Completion(context, CancellationToken.None))
{
    // Handle IContentResult (see result types above)
}
```

### Async Enumerable Extensions

The `AsyncEnumerableExtentions` class provides utilities for working with async streams:

```csharp
using SK.Ext;

var streams = new List<(int, IAsyncEnumerable<string>)>();
await foreach (var (taskId, item) in streams.MergeWithTaskId())
{
    Console.WriteLine($"Task {taskId}: {item}");
}
```

## Tests

```
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:"C:\Users\ypyl\projects\sk_ext\SK.Ext.Tests\TestResults\*\coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

## License

This project is licensed under the MIT License.
