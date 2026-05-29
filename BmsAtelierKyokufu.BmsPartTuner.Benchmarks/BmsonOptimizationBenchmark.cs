using System.IO;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

namespace BmsAtelierKyokufu.BmsPartTuner.Benchmarks;

[MemoryDiagnoser]
[MarkdownExporterAttribute.GitHub]
[JsonExporterAttribute.Full]
[SimpleJob(BenchmarkDotNet.Engines.RunStrategy.ColdStart, launchCount: 1, warmupCount: 1, iterationCount: 5)]
public class BmsonOptimizationBenchmark
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    private string _bmsonDir = string.Empty;
    private string _bmsonFilePath = string.Empty;
    private string _outputBmsFilePath = string.Empty;
    private string _optimizedBmsFilePath = string.Empty;

    [GlobalSetup]
    public void Setup()
    {
        Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        _bmsonDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "bmson_sample", "bmson");
        _bmsonFilePath = Path.Combine(_bmsonDir, "bmson.bmson");
        _outputBmsFilePath = Path.Combine(_bmsonDir, "benchmark_output.bms");
        _optimizedBmsFilePath = Path.Combine(_bmsonDir, "benchmark_output_optimized.bms");

        if (!File.Exists(_bmsonFilePath))
        {
            throw new FileNotFoundException($"Test bmson file not found at {_bmsonFilePath}");
        }
    }

    [Benchmark]
    public void OptimizeBmsonToBms()
    {
        // 1. bmson -> bms conversion
        string bmsonJson = File.ReadAllText(_bmsonFilePath);
        var bmsonData = JsonSerializer.Deserialize<BmsonFormat>(bmsonJson, Options);
        if (bmsonData == null) return;

        var timeCalc = new PulseToBmsTimeCalculator(bmsonData.Info?.Resolution ?? 240, bmsonData.Lines ?? []);
        var realTimeCalc = new PulseToRealTimeCalculator(bmsonData.Info?.Resolution ?? 240, bmsonData.Info?.InitBpm ?? 120, bmsonData.BpmEvents, bmsonData.StopEvents);

        using var sliceManager = new AudioSliceManager(_bmsonDir, throwOnMissingFile: false);
        var scoreGen = new BmsScoreGenerator(bmsonData, timeCalc, realTimeCalc, sliceManager, keyNotesOnly: false);

        string generatedBmsText = scoreGen.GenerateBmsText();
        File.WriteAllText(_outputBmsFilePath, generatedBmsText);

        // 2. Load generated BMS and optimize
        var defManager = new BmsDefinitionManager(_outputBmsFilePath);
        var fileList = defManager.CreateFileList();

        var options = new DefinitionReductionOptions
        {
            R2Threshold = 0.40f,
            StartDefinition = 1,
            EndDefinition = 3843,
            SelectedKeywords = []
        };

        var audioCache = new Dictionary<string, ICachedSoundData>();
        var reuse = new DefinitionReuse(fileList, audioCache, generatedBmsText);

        reuse.ReductDefinition(_outputBmsFilePath, _optimizedBmsFilePath, options, NormalizationMode.None);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (File.Exists(_outputBmsFilePath)) File.Delete(_outputBmsFilePath);
        if (File.Exists(_optimizedBmsFilePath)) File.Delete(_optimizedBmsFilePath);
    }
}
