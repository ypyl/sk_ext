using SK.Ext.Models.History;
using SK.Ext.Models.Plugin;
using SK.Ext.Models.Settings;

namespace SK.Ext.Models;

public record CompletionContext(CompletionSystemMessage SystemMessage, CompletionHistory History, CompletionSettings Settings, IEnumerable<ICompletionPlugin> Plugins);
