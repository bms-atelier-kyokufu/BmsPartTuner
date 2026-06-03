using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Common;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;
using BmsAtelierKyokufu.BmsPartTuner.UI.Services;
using BmsAtelierKyokufu.BmsPartTuner.UI.ViewModels;
using Moq;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.UI.ViewModels
{
    /// <summary>
    /// <see cref="MainViewModelTests"/> の動作を検証するテストクラス。
    /// </summary>
    public class MainViewModelTests
    {
        private class MainViewModelTestFixture : IDisposable
        {
            public BmsFamilyTestContext Context { get; }
            public string SettingsPath { get; }
            public Mock<IBmsOptimizationService> OptimizationServiceMock { get; } = new();
            public Mock<BmsAtelierKyokufu.BmsPartTuner.UseCases.IBmsOptimizationUseCase> OptimizationUseCaseMock { get; } = new();
            public Mock<IBmsonConversionService> BmsonConversionServiceMock { get; } = new();
            public Mock<BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Common.IFileSystemService> FileSystemServiceMock { get; } = new();
            public Mock<IUIThreadDispatcher> DispatcherMock { get; } = new();
            public Mock<IAudioPlayerFactory> AudioPlayerFactoryMock { get; } = new();
            public MainViewModel ViewModel { get; }

            public MainViewModelTestFixture()
            {
                Context = new BmsFamilyTestContext();
                SettingsPath = Path.Combine(Context.TempDirectory, "setting.json");

                FileSystemServiceMock.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
                DispatcherMock.Setup(d => d.InvokeAsync(It.IsAny<Action>()))
                    .Callback<Action>(action => action())
                    .Returns(Task.CompletedTask);

                var audioPreviewService = new AudioPreviewService(DispatcherMock.Object, AudioPlayerFactoryMock.Object);
                var instrumentDetectionService = new InstrumentNameDetectionService();
                var fileListViewModel = new FileListViewModel(audioPreviewService, instrumentDetectionService);
                var filterService = new FileListFilterService();
                var settingsService = new SettingsService(SettingsPath);
                var themeServiceMock = new Mock<ThemeService>();
                var licenseLoaderService = new LicenseLoaderService();

                ViewModel = new MainViewModel(
                    OptimizationServiceMock.Object,
                    OptimizationUseCaseMock.Object,
                    BmsonConversionServiceMock.Object,
                    FileSystemServiceMock.Object,
                    fileListViewModel,
                    audioPreviewService,
                    filterService,
                    settingsService,
                    themeServiceMock.Object,
                    licenseLoaderService);
            }

            public void Dispose()
            {
                ViewModel.Dispose();
                Context.Dispose();
            }
        }

        private static string GetDummyBmsonContent()
        {
            return @"{
                ""version"": ""1.0.0"",
                ""info"": {
                    ""title"": ""Test Chart"",
                    ""init_bpm"": 120.0,
                    ""resolution"": 240
                },
                ""lines"": [],
                ""bpm_events"": [],
                ""stop_events"": [],
                ""sound_channels"": []
            }";
        }

        /// <summary>
        /// OnInputPathChanged において、条件 WithBmsonFile の場合に DownconvertsAndLoadsWorkingBmsPath されることを検証します。
        /// </summary>
        [Fact]
        public Task OnInputPathChanged_WithBmsonFile_DownconvertsAndLoadsWorkingBmsPath()
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                // Arrange
                using var fixture = new MainViewModelTestFixture();
                var bmsonPath = Path.Combine(fixture.Context.TempDirectory, "test.bmson");
                File.WriteAllText(bmsonPath, GetDummyBmsonContent());

                // Act
                fixture.ViewModel.FileOperations.InputPath = bmsonPath;

                // Wait for downconversion task to run and finish
                int elapsed = 0;
                while (fixture.ViewModel.IsBusy && elapsed < 5000)
                {
                    await Task.Delay(50);
                    elapsed += 50;
                }

                // Assert
                Assert.Equal(bmsonPath, fixture.ViewModel.FileOperations.InputPath);
                Assert.Equal("準備完了", fixture.ViewModel.StatusMessage);
            });
        }

        /// <summary>
        /// OnInputPathChanged において、条件 WithBmsonFile の場合に NoValidationError されることを検証します。
        /// </summary>
        [Fact]
        public Task OnInputPathChanged_WithBmsonFile_NoValidationError()
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                // Arrange
                using var fixture = new MainViewModelTestFixture();
                var bmsonPath = Path.Combine(fixture.Context.TempDirectory, "test.bmson");
                File.WriteAllText(bmsonPath, GetDummyBmsonContent());

                // Act
                fixture.ViewModel.FileOperations.InputPath = bmsonPath;

                // Wait for downconversion task to run and finish
                int elapsed = 0;
                while (fixture.ViewModel.IsBusy && elapsed < 5000)
                {
                    await Task.Delay(50);
                    elapsed += 50;
                }

                // Assert
                var error = fixture.ViewModel["InputPath"];
                Assert.Equal(string.Empty, error);
            });
        }
    }
}
