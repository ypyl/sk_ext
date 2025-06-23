using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.AI.Evaluation.Reporting;

namespace SK.Ext.Eval;

/// <summary>
/// Stores evaluation results in a folder structure: rootPath/ScenarioName/Output/time/
/// </summary>
public sealed class DiskBasedResultStore : IEvaluationResultStore
{
    private readonly string _resultsRootPath;

    public DiskBasedResultStore(string rootPath)
    {
        rootPath = Path.GetFullPath(rootPath);
        _resultsRootPath = rootPath;
    }

    /// <summary>
    /// Stores the result object in the output folder for the scenario and timestamp.
    /// </summary>
    /// <param name="scenarioName">The scenario name.</param>
    /// <param name="result">The result object to serialize.</param>
    /// <param name="timestamp">The timestamp (e.g., DateTime.Now.ToString("yyyyMMddTHHmmss")) for the output folder.</param>
    /// <param name="fileName">The file name for the result (default: result.json).</param>
    public async Task StoreResultAsync(string scenarioName, object result, string timestamp, string fileName = "result.json", CancellationToken cancellationToken = default)
    {
        var outputDir = Path.Combine(_resultsRootPath, scenarioName, "Output", timestamp);
        Directory.CreateDirectory(outputDir);
        var resultPath = Path.Combine(outputDir, fileName);
        await using var stream = File.Create(resultPath);
        await JsonSerializer.SerializeAsync(stream, result, result.GetType(), new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
    }

    /// <summary>
    /// Reads all result files for a scenario, optionally filtered by timestamp.
    /// </summary>
    public async IAsyncEnumerable<T> ReadResultsAsync<T>(string scenarioName, string? timestamp = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var scenarioOutputDir = Path.Combine(_resultsRootPath, scenarioName, "Output");
        if (!Directory.Exists(scenarioOutputDir))
            yield break;

        IEnumerable<string> timeDirs = Directory.EnumerateDirectories(scenarioOutputDir);
        if (timestamp != null)
            timeDirs = timeDirs.Where(d => Path.GetFileName(d) == timestamp);

        foreach (var timeDir in timeDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var file in Directory.EnumerateFiles(timeDir, "*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await using var stream = File.OpenRead(file);
                var result = await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken);
                if (result != null)
                    yield return result;
            }
        }
    }

    /// <summary>
    /// Deletes all results for a scenario, or for a specific timestamp.
    /// </summary>
    public void DeleteResults(string scenarioName, string? timestamp = null)
    {
        var scenarioOutputDir = Path.Combine(_resultsRootPath, scenarioName, "Output");
        if (!Directory.Exists(scenarioOutputDir))
            return;
        if (timestamp == null)
        {
            Directory.Delete(scenarioOutputDir, recursive: true);
        }
        else
        {
            var timeDir = Path.Combine(scenarioOutputDir, timestamp);
            if (Directory.Exists(timeDir))
                Directory.Delete(timeDir, recursive: true);
        }
    }

    // Implement WriteResultsAsync for IEnumerable<ScenarioRunResult>
    public async ValueTask WriteResultsAsync(IEnumerable<ScenarioRunResult> results, CancellationToken cancellationToken = default)
    {
        foreach (var result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outputDir = Path.Combine(_resultsRootPath, result.ScenarioName, "Output", result.ExecutionName);
            Directory.CreateDirectory(outputDir);
            var resultPath = Path.Combine(outputDir, result.IterationName + ".json");
            await using var stream = File.Create(resultPath);
            await JsonSerializer.SerializeAsync(stream, result, result.GetType(), new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        }
    }

    // Implement ReadResultsAsync for ScenarioRunResult
    public async IAsyncEnumerable<ScenarioRunResult> ReadResultsAsync(string? executionName = null, string? scenarioName = null, string? iterationName = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IEnumerable<string> scenarioDirs = Directory.Exists(_resultsRootPath)
            ? Directory.EnumerateDirectories(_resultsRootPath)
            : Enumerable.Empty<string>();
        foreach (var scenarioDir in scenarioDirs)
        {
            if (scenarioName != null && Path.GetFileName(scenarioDir) != scenarioName)
                continue;
            var outputDir = Path.Combine(scenarioDir, "Output");
            if (!Directory.Exists(outputDir))
                continue;
            IEnumerable<string> execDirs = Directory.EnumerateDirectories(outputDir);
            foreach (var execDir in execDirs)
            {
                if (executionName != null && Path.GetFileName(execDir) != executionName)
                    continue;
                IEnumerable<string> files = Directory.EnumerateFiles(execDir, "*.json");
                foreach (var file in files)
                {
                    if (iterationName != null && Path.GetFileNameWithoutExtension(file) != iterationName)
                        continue;
                    cancellationToken.ThrowIfCancellationRequested();
                    await using var stream = File.OpenRead(file);
                    var result = await JsonSerializer.DeserializeAsync<ScenarioRunResult>(stream, cancellationToken: cancellationToken);
                    if (result != null)
                        yield return result;
                }
            }
        }
    }

    // Implement DeleteResultsAsync
    public ValueTask DeleteResultsAsync(string? executionName = null, string? scenarioName = null, string? iterationName = null, CancellationToken cancellationToken = default)
    {
        if (scenarioName == null)
        {
            if (Directory.Exists(_resultsRootPath))
            {
                Directory.Delete(_resultsRootPath, recursive: true);
                Directory.CreateDirectory(_resultsRootPath);
            }
        }
        else
        {
            var scenarioDir = Path.Combine(_resultsRootPath, scenarioName, "Output");
            if (!Directory.Exists(scenarioDir))
                return default;
            if (executionName == null)
            {
                Directory.Delete(scenarioDir, recursive: true);
            }
            else
            {
                var execDir = Path.Combine(scenarioDir, executionName);
                if (!Directory.Exists(execDir))
                    return default;
                if (iterationName == null)
                {
                    Directory.Delete(execDir, recursive: true);
                }
                else
                {
                    var file = Path.Combine(execDir, iterationName + ".json");
                    if (File.Exists(file))
                        File.Delete(file);
                }
            }
        }
        return default;
    }

    public async IAsyncEnumerable<string> GetLatestExecutionNamesAsync(int? count = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var execDirs = new List<(string scenario, string exec, DateTime created)>();
        if (Directory.Exists(_resultsRootPath))
        {
            foreach (var scenarioDir in Directory.EnumerateDirectories(_resultsRootPath))
            {
                var outputDir = Path.Combine(scenarioDir, "Output");
                if (!Directory.Exists(outputDir)) continue;
                foreach (var execDir in Directory.EnumerateDirectories(outputDir))
                {
                    var dirInfo = new DirectoryInfo(execDir);
                    execDirs.Add((Path.GetFileName(scenarioDir), Path.GetFileName(execDir), dirInfo.CreationTimeUtc));
                }
            }
        }
        var ordered = execDirs.OrderByDescending(e => e.created).Select(e => e.exec).Distinct();
        if (count.HasValue)
            ordered = ordered.Take(count.Value);
        foreach (var exec in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return exec;
        }
    }

    public async IAsyncEnumerable<string> GetScenarioNamesAsync(string executionName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (Directory.Exists(_resultsRootPath))
        {
            foreach (var scenarioDir in Directory.EnumerateDirectories(_resultsRootPath))
            {
                var outputDir = Path.Combine(scenarioDir, "Output", executionName);
                if (Directory.Exists(outputDir))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return Path.GetFileName(scenarioDir);
                }
            }
        }
    }

    public async IAsyncEnumerable<string> GetIterationNamesAsync(string executionName, string scenarioName, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var execDir = Path.Combine(_resultsRootPath, scenarioName, "Output", executionName);
        if (Directory.Exists(execDir))
        {
            foreach (var file in Directory.EnumerateFiles(execDir, "*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return Path.GetFileNameWithoutExtension(file);
            }
        }
    }
}
