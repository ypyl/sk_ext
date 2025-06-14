using SK.Ext.Models;
using SK.Ext.Models.History;
using SK.Ext.Models.Plugin;
using SK.Ext.Models.Settings;

namespace SK.Ext.Tests
{
    public class CompletionContextTests
    {
        [Fact]
        public void SwitchIdentity_WithSystemPrompt_ChangesHistoryAndSystemMessage()
        {
            // Arrange
            var systemMessage = new CompletionSystemMessage { Prompt = "Initial system prompt" };
            var userIdentity = ParticipantIdentity.User;
            var assistantIdentity = ParticipantIdentity.Assistant;
            var messages = new List<CompletionMessage>
            {
                new() {
                    Identity = userIdentity,
                },
                new() {
                    Identity = assistantIdentity,
                }
            };
            var history = new CompletionHistory().ForIdentity(userIdentity).AddMessages(messages);
            var settings = new CompletionSettings();
            var plugins = new List<ICompletionPlugin>();
            var context = new CompletionContext(systemMessage, history, settings, plugins);
            var newPrompt = "New system prompt";
            var newAsistantIdentity = new ParticipantIdentity("New Assistant", CompletionRole.Assistant);

            // Act
            var newContext = context.SwitchIdentity(newAsistantIdentity, newPrompt);

            // Assert
            Assert.NotSame(context, newContext);
            Assert.Equal(CompletionRole.User, newContext.History[0].Identity.Role);
            Assert.Equal(CompletionRole.User, newContext.History[1].Identity.Role);
            Assert.Equal(newPrompt, newContext.SystemMessage.Prompt);
        }
    }
}
