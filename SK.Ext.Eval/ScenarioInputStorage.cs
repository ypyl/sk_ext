using System.Text.Json;
using SK.Ext.Models.History;

namespace SK.Ext.Eval;

/// <summary>
/// Responsible for storing scenario data (system message, messages, response) in the Scenario/<scenarioName>/Input folder.
/// </summary>
public static class ScenarioInputStorage
{
    /// <summary>
    /// Stores the provided messages, system message, and response as scenario files in the Scenario/<scenarioName>/Input folder.
    /// </summary>
    /// <param name="scenarioName">The name of the scenario (subfolder under Scenario).</param>
    /// <param name="systemMessage">The system message to serialize to system.json.</param>
    /// <param name="messages">The messages to serialize to messages.json.</param>
    /// <param name="response">The response to serialize to response.json.</param>
    public static void StoreScenario(string scenarioName, CompletionSystemMessage systemMessage, IEnumerable<CompletionText> messages, CompletionText response)
    {
        var scenarioInputPath = Path.Combine("Scenario", scenarioName, "Input");
        Directory.CreateDirectory(scenarioInputPath);

        var messagesPath = Path.Combine(scenarioInputPath, "messages.json");
        var responsePath = Path.Combine(scenarioInputPath, "response.json");
        var systemPath = Path.Combine(scenarioInputPath, "system.json");

        var messagesJson = JsonSerializer.Serialize(messages, new JsonSerializerOptions { WriteIndented = true });
        var responseJson = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        var systemJson = JsonSerializer.Serialize(systemMessage, new JsonSerializerOptions { WriteIndented = true });

        File.WriteAllText(messagesPath, messagesJson);
        File.WriteAllText(responsePath, responseJson);
        File.WriteAllText(systemPath, systemJson);
    }
}
