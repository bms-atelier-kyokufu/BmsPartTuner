using System.IO;
using BmsAtelierKyokufu.BmsPartTuner.Services.Audio;
using BmsAtelierKyokufu.BmsPartTuner.Services.Audio.AudioPlayer;
using BmsAtelierKyokufu.BmsPartTuner.Services.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Services.Common;
using BmsAtelierKyokufu.BmsPartTuner.Services.UI;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers;
using BmsAtelierKyokufu.BmsPartTuner.Tests.Infrastructure;
using BmsAtelierKyokufu.BmsPartTuner.ViewModels;
using Moq;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.ViewModels
{
    public class MainViewModelTests
    {
        [Fact]
        public Task OnInputPathChanged_WithBmsonFile_DownconvertsAndLoadsWorkingBmsPath()
        {
            return WpfTestHelper.RunStaAsync(async () =>
            {
                // Arrange
                using var context = new BmsTestContext();
                var bmsonPath = Path.Combine(context.TempDirectory, "test.bmson");
                var bmsonContent = @"{
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
                File.WriteAllText(bmsonPath, bmsonContent);

                var optimizationServiceMock = new Mock<IBmsOptimizationService>();
                var dispatcherMock = new Mock<BmsAtelierKyokufu.BmsPartTuner.Services.UI.IUIThreadDispatcher>();
                var audioPlayerFactoryMock = new Mock<IAudioPlayerFactory>();

                // Execute UI Dispatcher immediately
                dispatcherMock.Setup(d => d.InvokeAsync(It.IsAny<Action>()))
                    .Callback<Action>(action => action())
                    .Returns(Task.CompletedTask);

                var audioPreviewService = new AudioPreviewService(dispatcherMock.Object, audioPlayerFactoryMock.Object);
                var instrumentDetectionService = new InstrumentNameDetectionService();
                var filterService = new FileListFilterService();

                var settingsPath = Path.Combine(context.TempDirectory, "setting.json");
                var settingsService = new SettingsService(settingsPath);
                var themeServiceMock = new Mock<ThemeService>();
                var licenseLoaderService = new LicenseLoaderService();

                using var viewModel = new MainViewModel(
                    optimizationServiceMock.Object,
                    audioPreviewService,
                    instrumentDetectionService,
                    filterService,
                    settingsService,
                    themeServiceMock.Object,
                    licenseLoaderService);

                // Act
                viewModel.InputPath = bmsonPath;

                // Wait for downconversion task to run and finish
                // Downconvert runs on Task.Run inside OnInputPathChanged. Since it's async void, we can check _isDownconverting state or wait briefly.
                int elapsed = 0;
                while (viewModel.IsBusy && elapsed < 5000)
                {
                    await Task.Delay(50);
                    elapsed += 50;
                }

                // Assert
                // Under Strategy B, InputPath remains showing the original .bmson file path
                Assert.Equal(bmsonPath, viewModel.InputPath);

                // The output bms file should exist
                var expectedBmsPath = Path.Combine(context.TempDirectory, "test_downconverted.bms");
                Assert.True(File.Exists(expectedBmsPath));
            });
        }
    }
}
