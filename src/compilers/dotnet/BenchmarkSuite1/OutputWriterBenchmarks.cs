namespace Bloqr.Compiler.Dotnet.Benchmarks;
[CPUUsageDiagnoser]
public class OutputWriterBenchmarks
{
    private string _testFilePath = null !;
    private string _destinationFilePath = null !;
    private OutputWriter _outputWriter = null !;
    [GlobalSetup]
    public void Setup()
    {
        _outputWriter = new OutputWriter(NullLogger<OutputWriter>.Instance);
        // Create a test file with realistic filter rules content
        _testFilePath = Path.Combine(Path.GetTempPath(), $"benchmark_rules_{Guid.NewGuid()}.txt");
        _destinationFilePath = Path.Combine(Path.GetTempPath(), $"benchmark_dest_{Guid.NewGuid()}.txt");
        var sb = new StringBuilder();
        // Add header comments
        sb.AppendLine("! Title: Test Filter List");
        sb.AppendLine("! Version: 1.0.0");
        sb.AppendLine("! Last modified: 2024-01-01");
        sb.AppendLine();
        // Add 10000 rules (mix of rules and comments)
        for (int i = 0; i < 10000; i++)
        {
            if (i % 10 == 0)
            {
                sb.AppendLine($"! Comment line {i}");
            }
            else if (i % 5 == 0)
            {
                sb.AppendLine(); // Empty line
            }
            else
            {
                sb.AppendLine($"||example{i}.com^");
            }
        }

        File.WriteAllText(_testFilePath, sb.ToString());
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_testFilePath))
            File.Delete(_testFilePath);
        if (File.Exists(_destinationFilePath))
            File.Delete(_destinationFilePath);
    }

    [Benchmark]
    public async Task<int> CountRulesAsync()
    {
        return await _outputWriter.CountRulesAsync(_testFilePath);
    }

    [Benchmark]
    public async Task<bool> CopyOutputAsync()
    {
        return await _outputWriter.CopyOutputAsync(_testFilePath, _destinationFilePath);
    }

    [Benchmark]
    public async Task<string> ComputeHashAsync()
    {
        return await _outputWriter.ComputeHashAsync(_testFilePath);
    }
}