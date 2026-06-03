using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;
using BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.UI.ViewModels;

/// <summary>
/// <see cref="FileOperationsViewModelTests"/> の動作を検証するテストクラス。
/// </summary>
public class FileOperationsViewModelTests
{
    /// <summary>
    /// OnInputPathChanged において、条件 WithBmsonFile の場合に ForcesBmsOutputExtension されることを検証します。
    /// </summary>
    [Fact]
    public void OnInputPathChanged_WithBmsonFile_ForcesBmsOutputExtension()
    {
        var audioCache = new System.Collections.Concurrent.ConcurrentDictionary<string, BmsAtelierKyokufu.BmsPartTuner.Models.ICachedSoundData>();
        using var context = new BmsFamilyTestContext();
        var bmsonPath = Path.Combine(context.TempDirectory, "test.bmson");
        File.WriteAllText(bmsonPath, "{}"); // Create empty bmson file

        var viewModel = new FileOperationsViewModel();
        string generatedOutputPath = "";
        viewModel.AutoOutputPathRequested += (_, path) => generatedOutputPath = path;

        // When InputPath is set to a .bmson file
        viewModel.InputPath = bmsonPath;

        // Then OutputPath should have the .bms extension (forced from .bmson)
        const string expectedOutputName = "test_optimized.bms";
        var actualOutputName = Path.GetFileName(generatedOutputPath);

        Assert.Equal(expectedOutputName, actualOutputName);
        Assert.EndsWith(".bms", generatedOutputPath);
    }
}
