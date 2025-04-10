# SK.Ext

`SK.Ext` is a .NET library that extends the functionality of the Microsoft Semantic Kernel. It provides additional utilities and extensions to enhance the development experience when working with Semantic Kernel.

## Features

- **Chat Completion Extensions**: Stream chat messages with functions.
- **Chat History Extensions**: Manage and manipulate chat history efficiently.

## Installation

To install `SK.Ext`, add the following package reference to your project file:

```xml
<PackageReference Include="SK.Ext" Version="1.0.0" />
```

Alternatively, you can install it via the .NET CLI:

```sh
dotnet add package SK.Ext --version 1.0.0
```

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
    // Handle the result
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

## Tests

```
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:"C:\Users\ypyl\projects\sk_ext\SK.Ext.Tests\TestResults\a9f93403-7773-424c-b38c-381a2f50e961\coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

## License

This project is licensed under the MIT License.
