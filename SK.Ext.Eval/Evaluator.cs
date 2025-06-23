using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using SK.Ext.Models.History;
using System.Text.Json;

namespace SK.Ext.Eval;

public class Evaluator(IChatClient chatClient)
{
    private IEnumerable<CompletionText>? loadedMessages;
    private CompletionText? loadedResponse;
    private CompletionSystemMessage? loadedSystemMessage;

    private ChatConfiguration GetChatConfiguration()
    {
        return new ChatConfiguration(chatClient);
    }

    private static string ExecutionName => $"{DateTime.Now:yyyyMMddTHHmmss}";

    private static ChatMessage MapToChatMessage(CompletionText completionText)
        => new(MapCompletionRoleToChatRole(completionText.Identity), completionText.Content);

    public enum EvalRating
    {
        Unknown,
        Poor,
        Fair,
        Good,
        Exceptional
    }

    public record EvalResult(bool Failed, EvalRating Rating, string? Reason);

    /// <summary>
    /// Loads scenario data from the Scenario folder for the given scenario name.
    /// </summary>
    /// <param name="scenarioName">The name of the scenario (subfolder under Scenario).</param>
    private void LoadScenario(string scenarioName)
    {
        var scenarioPath = Path.Combine("Scenario", scenarioName, "Input");
        var messagesPath = Path.Combine(scenarioPath, "messages.json");
        var responsePath = Path.Combine(scenarioPath, "response.json");
        var systemPath = Path.Combine(scenarioPath, "system.json");

        if (!File.Exists(messagesPath) || !File.Exists(responsePath) || !File.Exists(systemPath))
            throw new FileNotFoundException($"Scenario files not found for scenario: {scenarioName}");

        var messagesJson = File.ReadAllText(messagesPath);
        var responseJson = File.ReadAllText(responsePath);
        var systemJson = File.ReadAllText(systemPath);

        loadedMessages = JsonSerializer.Deserialize<List<CompletionText>>(messagesJson) ?? throw new InvalidOperationException("Failed to deserialize messages.json");
        loadedResponse = JsonSerializer.Deserialize<CompletionText>(responseJson) ?? throw new InvalidOperationException("Failed to deserialize response.json");
        loadedSystemMessage = JsonSerializer.Deserialize<CompletionSystemMessage>(systemJson) ?? throw new InvalidOperationException("Failed to deserialize system.json");
    }

    /// <summary>
    /// Runs the evaluation for the specified scenario name.
    /// </summary>
    /// <param name="scenarioName">The name of the scenario (subfolder under Scenario).</param>
    /// <returns>The evaluation result.</returns>
    public async Task<EvalResult> Run(string scenarioName, CancellationToken cancellationToken)
    {
        LoadScenario(scenarioName);

        if (loadedMessages is null || loadedResponse is null || loadedSystemMessage is null)
            throw new InvalidOperationException("Scenario data not loaded. Call LoadScenario first.");

        // Create a chat history from the system message and provided messages.
        var chatMessages = new List<ChatMessage>
        {
            new ChatMessage(ChatRole.System, loadedSystemMessage.Prompt)
        };
        chatMessages.AddRange(loadedMessages.Select(m => MapToChatMessage(m)));

        // Create a response from the provided response text.
        var responseMessage = new ChatResponse(MapToChatMessage(loadedResponse));

        var coherenceEvaluator = new CoherenceEvaluator();

        var resultStore = new DiskBasedResultStore("Scenario");
        IEvaluationResponseCacheProvider responseCacheProvider = new DiskBasedResponseCacheProvider("Scenario", null);
        ReportingConfiguration reportingConfiguration = new ReportingConfiguration(
            [coherenceEvaluator],
            resultStore,
            GetChatConfiguration(),
            responseCacheProvider,
            null, // cachingKeys
            ExecutionName
        );

        await using ScenarioRun scenarioRun =
            await reportingConfiguration.CreateScenarioRunAsync(
                scenarioName);

        /// Retrieve the score for coherence from the <see cref="EvaluationResult"/>.
        var result = await scenarioRun.EvaluateAsync(chatMessages, responseMessage, cancellationToken: cancellationToken); ;

        // Extract relevant values from the coherence metric.
        NumericMetric coherence = result.Get<NumericMetric>(CoherenceEvaluator.CoherenceMetricName);
        var interpretationFailed = coherence.Interpretation?.Failed ?? false;
        var rating = coherence.Interpretation?.Rating switch
        {
            EvaluationRating.Exceptional => EvalRating.Exceptional,
            EvaluationRating.Good => EvalRating.Good,
            EvaluationRating.Average => EvalRating.Fair,
            EvaluationRating.Poor => EvalRating.Poor,
            _ => EvalRating.Unknown
        };

        return new EvalResult(interpretationFailed, rating, coherence.Reason);
    }

    private static ChatRole MapCompletionRoleToChatRole(ParticipantIdentity identity)
    {
        return identity.Role switch
        {
            CompletionRole.User => ChatRole.User,
            CompletionRole.Assistant => ChatRole.Assistant,
            _ => throw new NotSupportedException($"Unsupported role: {identity.Role}")
        };
    }
}
