using SK.Ext.Models.Plugin;

namespace SK.Ext.Tests
{
    public class TestEchoPlugin : ICompletionPlugin
    {
        private bool _isRequired;
        public TestEchoPlugin(bool isRequired = false) { _isRequired = isRequired; }
        public Delegate FunctionDelegate => Echo;
        public string PluginName => "TestPlugin";
        public string Name => "Echo";
        public string FunctionDescription => "Echoes the input string.";
        public IEnumerable<PluginParameter> Parameters => new PluginParameter[]
        {
            new ModelParameter { Name = "input", Description = "Input string", Type = typeof(string), IsRequired = true },
            new RuntimeParameter { Name = "runtime", Value = "Runtime information" }
        };
        public PluginReturnMetadata Return => new PluginReturnMetadata { Description = "Echoed string", Type = typeof(string) };
        public bool IsRequired => _isRequired;

        public string Echo(string input, string runtime)
        {
            return input + runtime;
        }
    }
}
