using SK.Ext.Sample;

class Program
{
    const string groqKey = "<<KEY>>";

    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            Console.WriteLine("No sample name provided. Running default sample:.");
            await RunSample("completion");
            return;
        }

        string sampleName = args[0].ToLower();
        await RunSample(sampleName);
    }

    static async Task RunSample(string sampleName)
    {
        switch (sampleName)
        {
            case "duplication":
                await RemoveDuplicatedFunctionCallResultsSample.Run(groqKey);
                break;
            case "completion":
                await CompletionSample.Run(groqKey);
                break;
            case "streamed":
                await StreamedFunctionExecutionSample.Run(groqKey);
                break;
            case "parallel":
                await ParallelExecutionSample.Run(groqKey);
                break;
            case "structured":
                await StructuredOutputSample.Run(groqKey);
                break;
            case "workflow1":
                await WorkflowSample.Run(groqKey, "What is the biggest city by population in Europe?");
                break;
            case "workflow2":
                await WorkflowSample.Run(groqKey, "I need help optimizing a complex SQL database with millions of records. Consider indexing strategies, query performance, partitioning options, and maintenance plans. What should I do?");
                break;
            case "collab1":
                await CollaborationSample.Run(groqKey, "Write a comprehensive guide about machine learning basics.", default);
                break;
            case "collab2":
                await CollaborationSample.Run(groqKey, "Explain the SOLID principles in software development with examples.", default);
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
