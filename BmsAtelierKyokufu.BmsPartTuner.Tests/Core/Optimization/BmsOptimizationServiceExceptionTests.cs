using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Optimization;
using BmsAtelierKyokufu.BmsPartTuner.Models;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Core.Optimization;

/// <summary>
/// BmsOptimizationService の異常系、エラーハンドリング、警告処理に関するテスト。
/// </summary>
public class BmsOptimizationServiceExceptionTests : IDisposable
{
    private readonly BmsTestContext _context;
    private readonly BmsOptimizationService _service;
    private bool _disposed;

    public BmsOptimizationServiceExceptionTests()
    {
        _context = new BmsTestContext();
        _service = new BmsOptimizationService();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _context?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task RunDefinitionReductionTestAsync(ReductionTestOptions options)
    {
        var builder = _context.CreateBuilder();
        options.BuildBms?.Invoke(builder);
        string inputBmsPath = builder.Build("test.bms");
        string outputBmsPath = Path.Combine(_context.TempDirectory, "output.bms");
        var files = options.CreateFiles?.Invoke(_context.TempDirectory) ?? [];

        options.BeforeExecute?.Invoke(outputBmsPath);

        try
        {
            var result = await _service.ExecuteDefinitionReductionAsync(
                files,
                inputBmsPath,
                outputBmsPath,
                new DefinitionReductionOptions
                {
                    R2Threshold = options.Threshold ?? 0.95f,
                    StartDefinition = options.StartDef,
                    EndDefinition = options.EndDef,
                    IsPhysicalDeletionEnabled = options.PhysicalDeletion,
                    SelectedKeywords = options.Keywords
                });
            options.AssertResult?.Invoke(result);
        }
        finally
        {
            options.AfterExecute?.Invoke(outputBmsPath);
        }
    }

    private async Task RunOptimalThresholdsTestAsync(Func<string, List<string>> setupFiles, Action<OptimizationResult?> assertResult, int startDef = 1, int endDef = 1)
    {
        var files = setupFiles?.Invoke(_context.TempDirectory) ?? [];
        var result = await _service.FindOptimalThresholdsAsync(files, startDef, endDef);
        assertResult?.Invoke(result);
    }

    /// <summary>
    /// ExecuteDefinitionReductionAsync において、条件 InputFileNotFound の場合に ReturnsErrorResult されることを検証します。
    /// </summary>
    [Fact]
    public Task ExecuteDefinitionReductionAsync_InputFileNotFound_ReturnsErrorResult() =>
        RunDefinitionReductionTestAsync(new()
        {
            BuildBms = _ => { },
            CreateFiles = dir => [new() { Num = "01", NumInteger = 1, Name = BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "test.wav")) }],
            AssertResult = res => { Assert.NotNull(res); Assert.False(res.IsSuccess); Assert.NotNull(res.ErrorMessage); },
            BeforeExecute = _ => File.Delete(Path.Combine(_context.TempDirectory, "test.bms")) // Force file not found
        });

    /// <summary>
    /// ExecuteDefinitionReductionAsync において、条件 ReadOnlyOutputDirectory の場合に ReturnsErrorResult されることを検証します。
    /// </summary>
    [Fact]
    public Task ExecuteDefinitionReductionAsync_ReadOnlyOutputDirectory_ReturnsErrorResult() =>
        RunDefinitionReductionTestAsync(new()
        {
            BuildBms = b => b.WithHeader("TITLE", "Test").WithWav("01", "test.wav", false).AddMainData(11, "01"),
            CreateFiles = dir => { var f = BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "test.wav")); return [new() { Num = "01", NumInteger = 1, Name = f, FileSize = new FileInfo(f).Length }]; },
            AssertResult = res => { Assert.NotNull(res); Assert.False(res.IsSuccess); },
            BeforeExecute = outPath => { File.WriteAllText(outPath, "dummy"); File.SetAttributes(outPath, FileAttributes.ReadOnly); },
            AfterExecute = outPath => { try { File.SetAttributes(outPath, FileAttributes.Normal); File.Delete(outPath); } catch { } }
        });

    /// <summary>
    /// ExecuteDefinitionReductionAsync において、条件 PhysicalDeletionWithLockedFile の場合に ContinuesProcessing されることを検証します。
    /// </summary>
    [Fact]
    public async Task ExecuteDefinitionReductionAsync_PhysicalDeletionWithLockedFile_ContinuesProcessing()
    {
        var file1 = BmsTestWavHelper.CreateValidWavFile(Path.Combine(_context.TempDirectory, "locked.wav"));
        var file2 = BmsTestWavHelper.CreateValidWavFile(Path.Combine(_context.TempDirectory, "normal.wav"));

        await using var fs = new FileStream(file1, FileMode.Open, FileAccess.Read, FileShare.Read);

        await RunDefinitionReductionTestAsync(new()
        {
            BuildBms = b => b.WithHeader("TITLE", "Locked").WithWav("01", "locked.wav", false).WithWav("02", "normal.wav", false).AddMainData(11, "0102"),
            CreateFiles = _ => [
                new() { Num = "01", NumInteger = 1, Name = file1, FileSize = new FileInfo(file1).Length },
                new() { Num = "02", NumInteger = 2, Name = file2, FileSize = new FileInfo(file2).Length }
            ],
            AssertResult = res => Assert.True(res.IsSuccess),
            EndDef = 2,
            PhysicalDeletion = true
        });
    }

    /// <summary>
    /// FindOptimalThresholdsAsync において、条件 AllFilesNonExistent の場合に ReturnsNull されることを検証します。
    /// </summary>
    [Fact]
    public Task FindOptimalThresholdsAsync_AllFilesNonExistent_ReturnsNull() =>
        RunOptimalThresholdsTestAsync(dir => [Path.Combine(dir, "ghost1.wav"), Path.Combine(dir, "ghost2.wav")], Assert.Null, endDef: 10);

    /// <summary>
    /// FindOptimalThresholdsAsync において、条件 WithCorruptedWaveFiles の場合に ReturnsNull されることを検証します。
    /// </summary>
    [Fact]
    public Task FindOptimalThresholdsAsync_WithCorruptedWaveFiles_ReturnsNull() =>
        RunOptimalThresholdsTestAsync(
            dir => { var f = Path.Combine(dir, "corrupted.wav"); File.WriteAllText(f, "Invalid"); return [f]; },
            res => { Assert.NotNull(res); Assert.Equal(1, res.Base36Result.Count); Assert.Equal(1, res.Base62Result.Count); }
        );

    /// <summary>
    /// FindOptimalThresholdsAsync において、条件 WithCorruptedFiles の場合に ReturnsWarnings されることを検証します。
    /// </summary>
    [Fact]
    public Task FindOptimalThresholdsAsync_WithCorruptedFiles_ReturnsWarnings() =>
        RunOptimalThresholdsTestAsync(
            dir =>
            {
                var c1 = Path.Combine(dir, "corrupted1.wav"); File.WriteAllText(c1, "Invalid");
                var c2 = Path.Combine(dir, "corrupted2.wav"); File.WriteAllBytes(c2, []);
                return [BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "valid.wav")), c1, c2];
            },
            res => { Assert.NotNull(res); Assert.True(res.HasWarnings); Assert.NotEmpty(res.Warnings); Assert.Contains("2 件の", res.Warnings[0]); },
            endDef: 3
        );

    /// <summary>
    /// FindOptimalThresholdsAsync において、条件 WithMissingFiles の場合に SkipsNonExistentFiles されることを検証します。
    /// </summary>
    [Fact]
    public Task FindOptimalThresholdsAsync_WithMissingFiles_SkipsNonExistentFiles() =>
        RunOptimalThresholdsTestAsync(
            dir => [BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "valid1.wav")), BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "valid2.wav"), isDifferent: true), Path.Combine(dir, "missing.wav")],
            res => { Assert.NotNull(res); Assert.False(res.HasWarnings); Assert.Empty(res.Warnings); },
            endDef: 3
        );

    /// <summary>
    /// FindOptimalThresholdsAsync において、条件 WithSingleCorruptedFile の場合に ReturnsWarningWithFilename されることを検証します。
    /// </summary>
    [Fact]
    public Task FindOptimalThresholdsAsync_WithSingleCorruptedFile_ReturnsWarningWithFilename() =>
        RunOptimalThresholdsTestAsync(
            dir => { var c = Path.Combine(dir, "corrupted_single.wav"); File.WriteAllText(c, "Invalid"); return [BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "valid.wav")), c]; },
            res => { Assert.NotNull(res); Assert.True(res.HasWarnings); Assert.NotEmpty(res.Warnings); Assert.Contains("1 件の", res.Warnings[0]); Assert.Contains("corrupted_single.wav", res.Warnings[0]); },
            endDef: 2
        );

    /// <summary>
    /// FindOptimalThresholdsAsync において、条件 WithWarnings の場合に ProcessingContinuesSuccessfully されることを検証します。
    /// </summary>
    [Fact]
    public Task FindOptimalThresholdsAsync_WithWarnings_ProcessingContinuesSuccessfully() =>
        RunOptimalThresholdsTestAsync(
            dir =>
            {
                var c1 = Path.Combine(dir, "corrupted1.wav"); File.WriteAllText(c1, "Invalid");
                var c2 = Path.Combine(dir, "corrupted2.wav"); File.WriteAllText(c2, "Invalid");
                return [BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "valid1.wav")), BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "valid2.wav")), BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "valid3.wav"), true), c1, c2];
            },
            res => { Assert.NotNull(res); Assert.True(res.HasWarnings); Assert.NotEmpty(res.SimulationData); Assert.InRange(res.Base36Result.Count, 1, 5); Assert.Contains("2 件の", res.Warnings[0]); },
            endDef: 5
        );

    /// <summary>
    /// FindOptimalThresholdsAsync において、条件 WithLockedFile の場合に ReturnsWarning されることを検証します。
    /// </summary>
    [Fact]
    public async Task FindOptimalThresholdsAsync_WithLockedFile_ReturnsWarning()
    {
        var validFile = BmsTestWavHelper.CreateValidWavFile(Path.Combine(_context.TempDirectory, "valid.wav"));
        var lockedFile = BmsTestWavHelper.CreateValidWavFile(Path.Combine(_context.TempDirectory, "locked.wav"));
        await using var fs = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        await RunOptimalThresholdsTestAsync(
            _ => [validFile, lockedFile],
            res => { Assert.NotNull(res); Assert.True(res.HasWarnings); Assert.NotEmpty(res.Warnings); },
            endDef: 2
        );
    }

    /// <summary>
    /// FindOptimalThresholdsAsync において、条件 WarningCount の場合に MatchesFailedFileCount されることを検証します。
    /// </summary>
    [Fact]
    public Task FindOptimalThresholdsAsync_WarningCount_MatchesFailedFileCount() =>
        RunOptimalThresholdsTestAsync(
            dir =>
            {
                var c = Path.Combine(dir, "corrupted.wav"); File.WriteAllText(c, "Invalid");
                var z = Path.Combine(dir, "zero.wav"); File.WriteAllBytes(z, []);
                return [BmsTestWavHelper.CreateValidWavFile(Path.Combine(dir, "valid.wav")), c, Path.Combine(dir, "missing.wav"), z];
            },
            res => { Assert.NotNull(res); Assert.True(res.HasWarnings); Assert.Contains("2 件の", res.Warnings[0]); },
            endDef: 4
        );

    /// <summary>
    /// ExecuteDefinitionReductionAsync において、条件 ReadOnlyFile の場合に ContinuesWithoutCrash されることを検証します。
    /// </summary>
    [Fact]
    public Task ExecuteDefinitionReductionAsync_ReadOnlyFile_ContinuesWithoutCrash() =>
        RunDefinitionReductionTestAsync(new()
        {
            BuildBms = b => b.WithHeader("GENRE", "Test").WithWav(1, "used.wav", false).AddMainData(11, "01"),
            CreateFiles = dir =>
            {
                var f1 = Path.Combine(dir, "used.wav"); BmsTestWavHelper.CreateValidWavFile(f1);
                var f2 = Path.Combine(dir, "readonly_unused.wav"); BmsTestWavHelper.CreateValidWavFile(f2);
                return [new() { Name = f1, NumInteger = 1, Num = "01", FileSize = new FileInfo(f1).Length }, new() { Name = f2, NumInteger = 2, Num = "02", FileSize = new FileInfo(f2).Length }];
            },
            AssertResult = res => { Assert.True(res.IsSuccess); Assert.Equal(1, res.OptimizedCount); Assert.True(File.Exists(Path.Combine(_context.TempDirectory, "readonly_unused.wav"))); },
            BeforeExecute = _ => File.SetAttributes(Path.Combine(_context.TempDirectory, "readonly_unused.wav"), FileAttributes.ReadOnly),
            AfterExecute = _ => { if (File.Exists(Path.Combine(_context.TempDirectory, "readonly_unused.wav"))) File.SetAttributes(Path.Combine(_context.TempDirectory, "readonly_unused.wav"), FileAttributes.Normal); },
            Threshold = 0.99f,
            StartDef = 1,
            EndDef = 2,
            PhysicalDeletion = true
        });

    /// <summary>
    /// ExecuteDefinitionReductionAsync において、条件 MixedExistingAndMissing の場合に HandlesGracefully されることを検証します。
    /// </summary>
    [Fact]
    public Task ExecuteDefinitionReductionAsync_MixedExistingAndMissing_HandlesGracefully() =>
        RunDefinitionReductionTestAsync(new()
        {
            BuildBms = b => b.WithHeader("GENRE", "Test").WithWav(1, "existing.wav", false).AddMainData(11, "01"),
            CreateFiles = dir =>
            {
                var f = Path.Combine(dir, "existing.wav"); BmsTestWavHelper.CreateValidWavFile(f);
                return [new() { Name = f, NumInteger = 1, Num = "01", FileSize = new FileInfo(f).Length }, new() { Name = Path.Combine(dir, "missing.wav"), NumInteger = 2, Num = "02" }];
            },
            AssertResult = Assert.NotNull,
            Threshold = 0.99f,
            StartDef = 1,
            EndDef = 2,
            PhysicalDeletion = true
        });

    /// <summary>
    /// ExecuteDefinitionReductionAsync において、条件 WithException の場合に ClearsCache されることを検証します。
    /// </summary>
    [Fact]
    public async Task ExecuteDefinitionReductionAsync_WithException_ClearsCache()
    {
        // 1. レジストリにダミーデータを事前登録しておく
        VirtualAudioRegistry.AddFile("dummy_slice.wav", [1, 2, 3, 4]);

        var samples = new float[][] { [0.5f], [0.5f] };
        var prefixSum = new double[][] { [0.0, 0.5], [0.0, 0.5] };
        var prefixSumSq = new double[][] { [0.0, 0.25], [0.0, 0.25] };
        var signLsh = new ulong[][] { [1UL], [1UL] };
        var signLshMask = new ulong[][] { [1UL], [1UL] };
        var baseData = new BaseAudioOptimizationData(samples, prefixSum, prefixSumSq, signLsh, signLshMask);
        var pointerData = new PointerSoundData("dummy_slice.wav", baseData, 0, 1);
        AudioRegistry.Instance.Register("dummy_slice.wav", pointerData);

        // 2. 例外（入力ファイルなし）が発生するテストを実行
        await RunDefinitionReductionTestAsync(new()
        {
            BuildBms = _ => { },
            CreateFiles = _ => [new() { Name = "nonexistent.wav", Num = "01", NumInteger = 1 }],
            AssertResult = res => Assert.False(res.IsSuccess),
            BeforeExecute = _ => File.Delete(Path.Combine(_context.TempDirectory, "test.bms")),
            Threshold = 0.5f,
            StartDef = 1,
            EndDef = 1,
            PhysicalDeletion = false
        });

        // 3. 例外による脱出後、レジストリがクリアされていることを検証
        Assert.False(VirtualAudioRegistry.TryGetFileSize("dummy_slice.wav", out _));
        Assert.False(AudioRegistry.Instance.TryGet("dummy_slice.wav", out _));
    }
}
