using System.IO;
using System.Text;
using System.Text.Json;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;
using BmsAtelierKyokufu.BmsPartTuner.Services.Bms.Bmson;
using Xunit.Abstractions;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Scenarios;

public class BmsonOptimizationIntegrationTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    private readonly ITestOutputHelper _output;

    public BmsonOptimizationIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
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
        var bmsonData = JsonSerializer.Deserialize<BmsonFormat>(bmsonJson, Options);
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
        var oracle = JsonSerializer.Deserialize<OracleData>(oracleJson, Options);
        Assert.NotNull(oracle);

        // 1. bmson -> bms conversion
        string bmsonJson = File.ReadAllText(bmsonFilePath);
        var bmsonData = JsonSerializer.Deserialize<BmsonFormat>(bmsonJson, Options);
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

    [Fact]
    public void DiscoverHeuristicThreshold_FFT16D_R2_Relationship()
    {
        // Arrange
        string testDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "bmson_sample", "bms");
        string bmsFilePath = Path.Combine(testDataDir, "bmson_base62.bms");
        Assert.True(File.Exists(bmsFilePath), $"Test input file not found: {bmsFilePath}");

        var manager = new BmsDefinitionManager(bmsFilePath);
        var fileList = manager.CreateFileList();

        // Cache all files
        var audioCache = new Dictionary<string, ICachedSoundData>();
        string bmsDir = Path.GetDirectoryName(bmsFilePath) ?? "";
        foreach (var file in fileList)
        {
            if (file.NumInteger < 1 || file.NumInteger > 3843) continue;
            string fullPath = Path.Combine(bmsDir, file.Name);
            if (!File.Exists(fullPath)) continue;

            if (!audioCache.ContainsKey(file.Name))
            {
                var data = BmsAtelierKyokufu.BmsPartTuner.Core.Audio.AudioProcessingService.LoadAndProcess(fullPath, NormalizationMode.None);
                audioCache[file.Name] = data;
            }
        }

        var cachedList = audioCache.Values.ToList();
        _output.WriteLine($"Loaded {cachedList.Count} unique audio files.");

        // Extract 16D vectors
        var vectors = new Dictionary<string, float[]>();
        foreach (var kvp in audioCache)
        {
            var data = kvp.Value;
            var vec = new float[16];
            if (data.FftSpectrum != null && data.FftSpectrum.Length > 0 && data.FftSpectrum[0] != null)
            {
                var spec = data.FftSpectrum[0];
                double sumSq = 0;
                for (int i = 1; i <= 16; i++) // bins 1 to 16
                {
                    float mag = spec[i].Magnitude;
                    vec[i - 1] = mag;
                    sumSq += mag * mag;
                }

                // L2 Normalize the vector to ignore volume differences
                if (sumSq > 0)
                {
                    float norm = (float)Math.Sqrt(sumSq);
                    for (int i = 0; i < 16; i++)
                    {
                        vec[i] /= norm;
                    }
                }
            }
            vectors[kvp.Key] = vec;
        }

        float maxEuclideanForR2Match = 0f;
        int matchCount = 0;

        _output.WriteLine("R2_Score\tEuclidean_Dist\tFile1\tFile2");

        for (int i = 0; i < cachedList.Count; i++)
        {
            for (int j = i + 1; j < cachedList.Count; j++)
            {
                var data1 = cachedList[i];
                var data2 = cachedList[j];

                if (data1.Channels != data2.Channels || data1.SampleRate != data2.SampleRate) continue;

                int targetChannel = 0;
                if (data1.GetActiveRegions()[0] == null || data1.GetActiveRegions()[0].Count == 0) targetChannel = 1;

                var shorter = data1.TotalSamples < data2.TotalSamples ? data1 : data2;
                var longer = data1.TotalSamples < data2.TotalSamples ? data2 : data1;
                int shorterFrames = shorter.TotalSamples / shorter.Channels;
                int longerFrames = longer.TotalSamples / longer.Channels;
                var shorterSpan = shorter.GetRawSpan(targetChannel, 0, shorterFrames);
                var longerFullSpan = longer.GetRawSpan(targetChannel, 0, longerFrames);

                float r = BmsAtelierKyokufu.BmsPartTuner.Core.Audio.FastWaveCompare.CalculateMaxCorrelation(
                    shorter, longer, targetChannel, shorterFrames, longerFrames, shorterSpan, longerFullSpan, out _);

                var v1 = vectors[data1.FilePath];
                var v2 = vectors[data2.FilePath];

                if (v1.Length == 16 && v2.Length == 16)
                {
                    float distSq = 0;
                    for (int k = 0; k < 16; k++)
                    {
                        float diff = v1[k] - v2[k];
                        distSq += diff * diff;
                    }
                    float dist = (float)Math.Sqrt(distSq);

                    if (r >= 0.40f)
                    {
                        matchCount++;
                        if (dist > maxEuclideanForR2Match) maxEuclideanForR2Match = dist;
                        _output.WriteLine($"{r:F4}\t{dist:F4}\t{Path.GetFileName(data1.FilePath)}\t{Path.GetFileName(data2.FilePath)}");
                    }
                }
            }
        }

        _output.WriteLine($"---");
        _output.WriteLine($"Matches with R2 >= 0.40 : {matchCount}");
        _output.WriteLine($"Max Euclidean Distance for these matches: {maxEuclideanForR2Match:F4}");

        float suggestedThreshold = maxEuclideanForR2Match * 1.5f; // 50% safety margin
        _output.WriteLine($"Suggested Safe Distance Threshold (with 50% margin): {suggestedThreshold:F4}");
    }

    [Fact]
    public void DiscoverHeuristicThreshold_SimHash256_R2_Relationship()
    {
        // Arrange
        string testDataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "bmson_sample", "bms");
        string bmsFilePath = Path.Combine(testDataDir, "bmson_base62.bms");
        Assert.True(File.Exists(bmsFilePath), $"Test input file not found: {bmsFilePath}");

        var manager = new BmsDefinitionManager(bmsFilePath);
        var fileList = manager.CreateFileList();

        // Cache all files
        var audioCache = new Dictionary<string, ICachedSoundData>();
        string bmsDir = Path.GetDirectoryName(bmsFilePath) ?? "";
        foreach (var file in fileList)
        {
            if (file.NumInteger < 1 || file.NumInteger > 3843) continue;
            string fullPath = Path.Combine(bmsDir, file.Name);
            if (!File.Exists(fullPath)) continue;

            if (!audioCache.ContainsKey(file.Name))
            {
                var data = BmsAtelierKyokufu.BmsPartTuner.Core.Audio.AudioProcessingService.LoadAndProcess(fullPath, NormalizationMode.None);
                audioCache[file.Name] = data;
            }
        }

        var cachedList = audioCache.Values.ToList();
        _output.WriteLine($"Loaded {cachedList.Count} unique audio files.");

        int maxHammingForR2Match = 0;
        int matchCount = 0;

        _output.WriteLine("R2_Score\tHamming\tFile1\tFile2");

        for (int i = 0; i < cachedList.Count; i++)
        {
            for (int j = i + 1; j < cachedList.Count; j++)
            {
                var data1 = cachedList[i];
                var data2 = cachedList[j];

                if (data1.Channels != data2.Channels || data1.SampleRate != data2.SampleRate) continue;
                if (data1.SimHash256 == null || data2.SimHash256 == null) continue;

                int targetChannel = 0;
                if (data1.GetActiveRegions()[0] == null || data1.GetActiveRegions()[0].Count == 0) targetChannel = 1;

                var shorter = data1.TotalSamples < data2.TotalSamples ? data1 : data2;
                var longer = data1.TotalSamples < data2.TotalSamples ? data2 : data1;
                int shorterFrames = shorter.TotalSamples / shorter.Channels;
                int longerFrames = longer.TotalSamples / longer.Channels;
                var shorterSpan = shorter.GetRawSpan(targetChannel, 0, shorterFrames);
                var longerFullSpan = longer.GetRawSpan(targetChannel, 0, longerFrames);

                float r = BmsAtelierKyokufu.BmsPartTuner.Core.Audio.FastWaveCompare.CalculateMaxCorrelation(
                    shorter, longer, targetChannel, shorterFrames, longerFrames, shorterSpan, longerFullSpan, out _);

                var s1 = data1.SimHash256;
                var s2 = data2.SimHash256;

                int hammingDistance =
                    System.Numerics.BitOperations.PopCount(s1[0] ^ s2[0]) +
                    System.Numerics.BitOperations.PopCount(s1[1] ^ s2[1]) +
                    System.Numerics.BitOperations.PopCount(s1[2] ^ s2[2]) +
                    System.Numerics.BitOperations.PopCount(s1[3] ^ s2[3]);

                if (r >= 0.40f)
                {
                    matchCount++;
                    if (hammingDistance > maxHammingForR2Match) maxHammingForR2Match = hammingDistance;
                    _output.WriteLine($"{r:F4}\t{hammingDistance}\t{Path.GetFileName(data1.FilePath)}\t{Path.GetFileName(data2.FilePath)}");
                }
            }
        }

        _output.WriteLine($"---");
        _output.WriteLine($"Matches with R2 >= 0.40 : {matchCount}");
        _output.WriteLine($"Max Hamming Distance for these matches: {maxHammingForR2Match}");

        Assert.True(maxHammingForR2Match <= 64, $"Threshold 64 is too strict! Max observed was {maxHammingForR2Match}");
    }
}
