using System.IO;
using System.Text.Json;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;
using BmsAtelierKyokufu.BmsPartTuner.Services.Bms.Bmson;
using Xunit.Abstractions;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Scenarios;

public class BmsonOptimizationIntegrationTests
{
    private readonly ITestOutputHelper _output;

    public BmsonOptimizationIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
    }

    [Fact]
    public void DefinitionReuse_ShouldMatchV1_0_0_0_Output()
    {
        // Arrange
        string testDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "bmson_sample", "bms");
        string bmsFilePath = Path.Combine(testDataDir, "bmson_base62.bms");
        string expectedBmsFilePath = Path.Combine(testDataDir, "bmson_base62_optimized.bms");
        string outputBmsFilePath = Path.Combine(testDataDir, "bmson_base62_test_output.bms");

        Assert.True(File.Exists(bmsFilePath), $"Test input file not found: {bmsFilePath}");
        Assert.True(File.Exists(expectedBmsFilePath), $"Expected output file not found: {expectedBmsFilePath}");

        // Load files using BmsDefinitionManager
        var manager = new BmsDefinitionManager(bmsFilePath);
        var fileList = manager.CreateFileList();

        // 許容度 40% (0.40)
        var options = new DefinitionReductionOptions
        {
            R2Threshold = 0.40f,
            StartDefinition = 1,
            EndDefinition = 3843, // 62進数の最大値
            SelectedKeywords = []
        };

        var audioCache = new Dictionary<string, ICachedSoundData>();
        var reuse = new DefinitionReuse(fileList, audioCache);

        // Act
        reuse.ReductDefinition(bmsFilePath, outputBmsFilePath, options, NormalizationMode.None);

        // Assert
        Assert.True(File.Exists(outputBmsFilePath), "Output file was not generated.");

        string expectedText = File.ReadAllText(expectedBmsFilePath);
        string actualText = File.ReadAllText(outputBmsFilePath);

        // Remove carriage returns to avoid CRLF vs LF issues
        expectedText = expectedText.Replace("\r\n", "\n");
        actualText = actualText.Replace("\r\n", "\n");

        // The file names and sequence should be identical to v1.0.0.0
        // If there's a bug in comparison (like bypassing), the merge results will be different
        Assert.Equal(expectedText, actualText);

        // Cleanup
        if (File.Exists(outputBmsFilePath))
        {
            File.Delete(outputBmsFilePath);
        }
    }

    [Fact]
    public void BmsonToBms_Optimization_IntegrationTest()
    {
        // Arrange
        string bmsonDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "bmson_sample", "bmson");
        string bmsonFilePath = Path.Combine(bmsonDir, "bmson.bmson");
        string outputBmsFilePath = Path.Combine(bmsonDir, "bmson_test_output.bms");
        string optimizedBmsFilePath = Path.Combine(bmsonDir, "bmson_test_output_optimized.bms");

        Assert.True(File.Exists(bmsonFilePath), $"Test input file not found: {bmsonFilePath}");

        // 1. bmson -> bms conversion
        string bmsonJson = File.ReadAllText(bmsonFilePath);
        var bmsonData = JsonSerializer.Deserialize<BmsonFormat>(bmsonJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(bmsonData);

        var timeCalc = new PulseToBmsTimeCalculator(bmsonData.Info?.Resolution ?? 240, bmsonData.Lines ?? []);
        var realTimeCalc = new PulseToRealTimeCalculator(bmsonData.Info?.Resolution ?? 240, bmsonData.Info?.InitBpm ?? 120, bmsonData.BpmEvents, bmsonData.StopEvents);

        // This will create slice wav files in bmsonDir
        using var sliceManager = new AudioSliceManager(bmsonDir, throwOnMissingFile: false);
        var scoreGen = new BmsScoreGenerator(bmsonData, timeCalc, realTimeCalc, sliceManager, keyNotesOnly: false);

        string generatedBmsText = scoreGen.GenerateBmsText();
        File.WriteAllText(outputBmsFilePath, generatedBmsText);

        // 2. Load generated BMS and optimize
        var defManager = new BmsDefinitionManager(outputBmsFilePath);
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

        // Act
        reuse.ReductDefinition(outputBmsFilePath, optimizedBmsFilePath, options, NormalizationMode.None);

        // Assert
        Assert.True(File.Exists(optimizedBmsFilePath));
        string finalBms = File.ReadAllText(optimizedBmsFilePath);

        // Count #WAV definitions
        int wavCount = 0;
        using (var reader = new StringReader(finalBms))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("#WAV", StringComparison.OrdinalIgnoreCase))
                {
                    wavCount++;
                }
            }
        }

        _output.WriteLine($"Generated WAV count: {wavCount}");

        // In the original bmson_base62_optimized.bms there are exactly 29 WAVs defined.
        // If our logic works without bugs (not over-merging), it should be in the ballpark of 29.
        Assert.InRange(wavCount, 10, 50);

        // Cleanup
        if (File.Exists(outputBmsFilePath)) File.Delete(outputBmsFilePath);
        if (File.Exists(optimizedBmsFilePath)) File.Delete(optimizedBmsFilePath);
    }

    [Fact]
    public void OracleValidation_ShouldPass()
    {
        // Arrange
        string bmsonDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "bmson_sample", "bmson");
        string bmsonFilePath = Path.Combine(bmsonDir, "bmson.bmson");
        string oracleFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "bmson_sample", "oracle_bms_clef_decoy.json");
        string outputBmsFilePath = Path.Combine(bmsonDir, "oracle_test_output.bms");
        string optimizedBmsFilePath = Path.Combine(bmsonDir, "oracle_test_output_optimized.bms");

        Assert.True(File.Exists(bmsonFilePath), $"Test input file not found: {bmsonFilePath}");
        Assert.True(File.Exists(oracleFilePath), $"Oracle file not found: {oracleFilePath}");

        // Load Oracle
        string oracleJson = File.ReadAllText(oracleFilePath);
        var oracle = JsonSerializer.Deserialize<OracleData>(oracleJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(oracle);

        // 1. bmson -> bms conversion
        string bmsonJson = File.ReadAllText(bmsonFilePath);
        var bmsonData = JsonSerializer.Deserialize<BmsonFormat>(bmsonJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(bmsonData);

        var timeCalc = new PulseToBmsTimeCalculator(bmsonData.Info?.Resolution ?? 240, bmsonData.Lines ?? []);
        var realTimeCalc = new PulseToRealTimeCalculator(bmsonData.Info?.Resolution ?? 240, bmsonData.Info?.InitBpm ?? 120, bmsonData.BpmEvents, bmsonData.StopEvents);

        using var sliceManager = new AudioSliceManager(bmsonDir, throwOnMissingFile: false);
        var scoreGen = new BmsScoreGenerator(bmsonData, timeCalc, realTimeCalc, sliceManager, keyNotesOnly: false);
        string generatedBmsText = scoreGen.GenerateBmsText();
        File.WriteAllText(outputBmsFilePath, generatedBmsText);

        // 2. Load generated BMS and optimize
        var defManager = new BmsDefinitionManager(outputBmsFilePath);
        var fileList = defManager.CreateFileList();

        var options = new DefinitionReductionOptions
        {
            R2Threshold = 0.40f,
            StartDefinition = 1,
            EndDefinition = 3843,
            SelectedKeywords = ["Kick", "Clap", "Snare", "HiHat", "Piano"]
        };

        var audioCache = new Dictionary<string, ICachedSoundData>();
        var reuse = new DefinitionReuse(fileList, audioCache, generatedBmsText);
        reuse.ReductDefinition(outputBmsFilePath, optimizedBmsFilePath, options, NormalizationMode.None);

        Assert.True(File.Exists(optimizedBmsFilePath));
        string finalBms = File.ReadAllText(optimizedBmsFilePath);

        // Extract surviving WAVs
        var keptWavs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var reader = new StringReader(finalBms))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("#WAV", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        keptWavs.Add(parts[1].Trim());
                    }
                }
            }
        }

        // 3. Assert against Oracle
        var failedClusters = new List<string>();

        foreach (var cluster in oracle.ExpectedClusters)
        {
            var allSurviving = cluster.SourceWavIds
                .Where(src => keptWavs.Contains(src))
                .ToList();

            if (allSurviving.Count == 0)
            {
                failedClusters.Add($"Cluster '{cluster.LogicalGroupId}' failed. None of the source files survived (they were incorrectly merged into a different cluster).");
            }
            else if (allSurviving.Count > 1)
            {
                _output.WriteLine($"[INFO] Cluster '{cluster.LogicalGroupId}' had multiple unmerged sources due to mechanical differences: {string.Join(", ", allSurviving)}");
            }
        }

        foreach (var fail in failedClusters)
        {
            _output.WriteLine(fail);
        }

        // This assertion will FAIL initially, showing exactly which clusters failed.
        Assert.Empty(failedClusters);

        // Cleanup
        if (File.Exists(outputBmsFilePath)) File.Delete(outputBmsFilePath);
        if (File.Exists(optimizedBmsFilePath)) File.Delete(optimizedBmsFilePath);
    }
}
