using BmsAtelierKyokufu.BmsPartTuner.Services;
using BmsAtelierKyokufu.BmsPartTuner.Services.AudioPlayer;
using Moq;

namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Services;

public class AudioPreviewServiceTests
{
    private readonly Mock<IAudioPlayerFactory> _factoryMock;
    private readonly Mock<IAudioPlayer> _playerMock1;
    private readonly Mock<IAudioPlayer> _playerMock2;
    private readonly Mock<IUIThreadDispatcher> _dispatcherMock;
    private readonly AudioPreviewService _service;

    public AudioPreviewServiceTests()
    {
        _factoryMock = new Mock<IAudioPlayerFactory>();
        _playerMock1 = new Mock<IAudioPlayer>();
        _playerMock2 = new Mock<IAudioPlayer>();

        // Setup factory to return player1 then player2
        _factoryMock.SetupSequence(f => f.CreatePlayer())
            .Returns(_playerMock1.Object)
            .Returns(_playerMock2.Object);

        _dispatcherMock = new Mock<IUIThreadDispatcher>();
        // Make dispatcher execute action immediately
        _dispatcherMock.Setup(d => d.InvokeAsync(It.IsAny<Action>()))
            .Callback<Action>(a => a.Invoke())
            .Returns(Task.CompletedTask);

        _service = new AudioPreviewService(_dispatcherMock.Object, _factoryMock.Object);
    }

    [Fact]
    public async Task PreviewAudioAsync_StopsPreviousPlayback_BeforePlayingNew()
    {
        // Arrange
        var file1 = "file1.wav";
        var file2 = "file2.wav";

        // Act 1: Play first file
        await _service.PreviewAudioAsync(file1);

        // Wait for debounce
        await Task.Delay(400);

        // Verify player 1 was used
        _factoryMock.Verify(f => f.CreatePlayer(), Times.Once);
        _playerMock1.Verify(p => p.Play(file1), Times.Once);

        // Act 2: Play second file
        await _service.PreviewAudioAsync(file2);

        // Note: PreviewAudioAsync calls StopCurrentPlayback immediately at the beginning

        // Verify player 1 was stopped/disposed
        _playerMock1.Verify(p => p.Stop(), Times.Once);
        _playerMock1.Verify(p => p.Dispose(), Times.Once);

        // Wait for debounce again
        await Task.Delay(400);

        // Verify player 2 was used
        _factoryMock.Verify(f => f.CreatePlayer(), Times.Exactly(2));
        _playerMock2.Verify(p => p.Play(file2), Times.Once);
    }

    [Fact]
    public async Task PreviewAudioAsync_HandlesException_Gracefully()
    {
        // Arrange
        var file = "corrupt.wav";
        _playerMock1.Setup(p => p.Play(It.IsAny<string>())).Throws(new Exception("Corrupt file"));

        // Act
        // Should not throw
        await _service.PreviewAudioAsync(file);

        await Task.Delay(400);

        // Verify state changed to error (implicitly via event, but here checking no crash)
        _playerMock1.Verify(p => p.Play(file), Times.Once);
    }
}
