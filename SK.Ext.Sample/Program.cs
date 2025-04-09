using SK.Ext.Sample;

class Program
{
    const string groqKey = "<<KEY>>";

    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            Console.WriteLine("No sample name provided. Running default sample: parallel.");
            await new ParallelExecutionSample().Run(groqKey);
            return;
        }

        string sampleName = args[0].ToLower();
        await RunSample(sampleName);
    }

    static async Task RunSample(string sampleName)
    {
        switch (sampleName)
        {
            case "streamed":
                await new StreamedFunctionExecutionSample().Run(groqKey);
                break;
            case "parallel":
                await new ParallelExecutionSample().Run(groqKey);
                break;
            case "structured":
                await new StructuredOutputSample().Run(groqKey);
                break;
            case "workflow1":
                await new WorkflowSample().Run(groqKey, "What is the biggest city by population in Europe?");
                break;
            case "workflow2":
                await new WorkflowSample().Run(groqKey, "I need help optimizing a complex SQL database with millions of records. Consider indexing strategies, query performance, partitioning options, and maintenance plans. What should I do?");
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
    }
}
