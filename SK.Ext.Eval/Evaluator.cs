using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Quality;
using SK.Ext.Models.History;

namespace SK.Ext.Eval;

public class Evaluator(IChatClient chatClient)
{
    private ChatConfiguration GetChatConfiguration()
    {
        return new ChatConfiguration(chatClient);
    }

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

    public async Task<EvalResult> Eval(IEnumerable<CompletionText> messages, CompletionText response)
    {
        // Create a chat history from the provided messages.
        var chatMessages = messages.Select(m => MapToChatMessage(m));

        // Create a response from the provided response text.
        var responseMessage = new ChatResponse(MapToChatMessage(response));

        var coherenceEvaluator = new CoherenceEvaluator();
        EvaluationResult result = await coherenceEvaluator.EvaluateAsync(
            chatMessages,
            responseMessage,
            GetChatConfiguration());

        /// Retrieve the score for coherence from the <see cref="EvaluationResult"/>.
        NumericMetric coherence = result.Get<NumericMetric>(CoherenceEvaluator.CoherenceMetricName);

        // Extract relevant values from the coherence metric.
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
