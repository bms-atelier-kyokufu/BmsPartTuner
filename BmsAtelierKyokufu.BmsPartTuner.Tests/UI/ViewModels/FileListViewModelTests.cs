using System.Collections.ObjectModel;
using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms;
using BmsAtelierKyokufu.BmsPartTuner.UI.Services;
using BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;
using Moq;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.UI.ViewModels;

/// <summary>
/// <see cref="FileListViewModel"/> の動作を検証するテストクラス。
/// </summary>
public class FileListViewModelTests
{
    private readonly Mock<IUIThreadDispatcher> _dispatcherMock = new();
    private readonly Mock<IAudioPlayerFactory> _audioPlayerFactoryMock = new();
    private readonly AudioPreviewService _audioPreviewService;
    private readonly InstrumentNameDetectionService _instrumentDetectionService;

    public FileListViewModelTests()
    {
        _dispatcherMock.Setup(d => d.InvokeAsync(It.IsAny<Action>()))
            .Callback<Action>(action => action())
            .Returns(Task.CompletedTask);

        _audioPreviewService = new AudioPreviewService(_dispatcherMock.Object, _audioPlayerFactoryMock.Object);
        _instrumentDetectionService = new InstrumentNameDetectionService();
    }

    [Fact]
    public void ToggleChipSelection_MultiSelectionMode_TogglesIndependently()
    {
        // Arrange
        var viewModel = new FileListViewModel(_audioPreviewService, _instrumentDetectionService)
        {
            IsSingleChipSelection = false
        };

        var chip1 = new FileListFilterService.SelectableFilterChip { Keyword = "kick", IsSelected = true };
        var chip2 = new FileListFilterService.SelectableFilterChip { Keyword = "snare", IsSelected = true };
        viewModel.FilterChips = [chip1, chip2];

        // Act & Assert
        viewModel.ToggleChipSelection(chip1);
        Assert.False(chip1.IsSelected);
        Assert.True(chip2.IsSelected);

        viewModel.ToggleChipSelection(chip2);
        Assert.False(chip1.IsSelected);
        Assert.False(chip2.IsSelected);
    }

    [Fact]
    public void ToggleChipSelection_SingleSelectionMode_EnforcesSingleSelection()
    {
        // Arrange
        var viewModel = new FileListViewModel(_audioPreviewService, _instrumentDetectionService)
        {
            IsSingleChipSelection = true
        };

        var chip1 = new FileListFilterService.SelectableFilterChip { Keyword = "kick", IsSelected = false };
        var chip2 = new FileListFilterService.SelectableFilterChip { Keyword = "snare", IsSelected = false };
        viewModel.FilterChips = [chip1, chip2];

        // Act: Select chip1
        viewModel.ToggleChipSelection(chip1);
        Assert.True(chip1.IsSelected);
        Assert.False(chip2.IsSelected);

        // Act: Select chip2
        viewModel.ToggleChipSelection(chip2);
        Assert.False(chip1.IsSelected);
        Assert.True(chip2.IsSelected);

        // Act: Deselect chip2 (clicked again)
        viewModel.ToggleChipSelection(chip2);
        Assert.False(chip1.IsSelected);
        Assert.False(chip2.IsSelected);
    }

    [Fact]
    public void OnIsSingleChipSelectionChanged_FromFalseToTrue_ReducesToFirstSelected()
    {
        // Arrange
        var viewModel = new FileListViewModel(_audioPreviewService, _instrumentDetectionService)
        {
            IsSingleChipSelection = false
        };

        var chip1 = new FileListFilterService.SelectableFilterChip { Keyword = "kick", IsSelected = true };
        var chip2 = new FileListFilterService.SelectableFilterChip { Keyword = "snare", IsSelected = true };
        var chip3 = new FileListFilterService.SelectableFilterChip { Keyword = "hat", IsSelected = true };
        viewModel.FilterChips = [chip1, chip2, chip3];

        // Act
        viewModel.IsSingleChipSelection = true;

        // Assert: Only chip1 remains selected
        Assert.True(chip1.IsSelected);
        Assert.False(chip2.IsSelected);
        Assert.False(chip3.IsSelected);
    }

    [Fact]
    public void OnFilterChipsChanged_WhenInSingleSelectionMode_ReducesToFirstSelected()
    {
        // Arrange
        var viewModel = new FileListViewModel(_audioPreviewService, _instrumentDetectionService)
        {
            IsSingleChipSelection = true
        };

        var chip1 = new FileListFilterService.SelectableFilterChip { Keyword = "kick", IsSelected = true };
        var chip2 = new FileListFilterService.SelectableFilterChip { Keyword = "snare", IsSelected = true };
        var newChips = new ObservableCollection<FileListFilterService.SelectableFilterChip> { chip1, chip2 };

        // Act
        viewModel.FilterChips = newChips;

        // Assert: Only chip1 remains selected
        Assert.True(chip1.IsSelected);
        Assert.False(chip2.IsSelected);
    }
}
