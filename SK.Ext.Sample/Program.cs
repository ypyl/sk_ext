using SK.Ext.Sample;

class Program
{
    const string defaultSample = "identity-collaboration";

    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            Console.WriteLine($"No sample name provided. Running default sample: {defaultSample}.");
            await RunSample(defaultSample);
            return;
        }

        string sampleName = args[0].ToLower();
        await RunSample(sampleName);
    }

    static async Task RunSample(string sampleName)
    {
        switch (sampleName)
        {
            case "runtime":
                await RuntimeSample.Run(GroqConfig.Key);
                break;
            case "full-answer":
                await FullAnswerSample.Run(GroqConfig.Key);
                break;
            case "identity-collaboration":
                await IdentityCollaboration.Run(GroqConfig.Key);
                break;
            case "duplication":
                await RemoveDuplicatedFunctionCallResultsSample.Run(GroqConfig.Key);
                break;
            case "completion":
                await CompletionSample.Run(GroqConfig.Key);
                break;
            case "streamed":
                await StreamedFunctionExecutionSample.Run(GroqConfig.Key);
                break;
            case "parallel":
                await ParallelExecutionSample.Run(GroqConfig.Key);
                break;
            case "structured":
                await StructuredOutputSample.Run(GroqConfig.Key);
                break;
            case "workflow1":
                await WorkflowSample.Run(GroqConfig.Key, "What is the biggest city by population in Europe?");
                break;
            case "workflow2":
                await WorkflowSample.Run(GroqConfig.Key, "I need help optimizing a complex SQL database with millions of records. Consider indexing strategies, query performance, partitioning options, and maintenance plans. What should I do?");
                break;
            case "collab1":
                await CollaborationSample.Run(GroqConfig.Key, "Write a comprehensive guide about machine learning basics.", default);
                break;
            case "collab2":
                await CollaborationSample.Run(GroqConfig.Key, "Explain the SOLID principles in software development with examples.", default);
                break;
            default:
                Console.WriteLine($"Unknown sample: {sampleName}");
                PrintUsage();
                break;
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run <sample-name>");
        Console.WriteLine("Available samples:");
        Console.WriteLine("  parallel    - Run parallel execution sample");
        Console.WriteLine("  structured  - Run structured output sample");
        Console.WriteLine("  workflow1   - Run workflow sample with city population query");
        Console.WriteLine("  workflow2   - Run workflow sample with SQL optimization query");
        Console.WriteLine("  collab1     - Run collaboration sample with ML basics guide");
        Console.WriteLine("  collab2     - Run collaboration sample with SOLID principles explanation");
    }
}
