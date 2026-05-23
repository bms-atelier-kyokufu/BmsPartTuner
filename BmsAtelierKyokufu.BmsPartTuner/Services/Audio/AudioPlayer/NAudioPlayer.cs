using NAudio.Wave;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Audio.AudioPlayer;

/// <summary>
/// NAudio implementation of IAudioPlayer.
/// </summary>
public class NAudioPlayer : IAudioPlayer
{
    private WaveOutEvent? _waveOut;
    private WaveStream? _audioReader;
    private Stream? _memoryStreamToDispose;

    public event EventHandler? PlaybackStopped;

    public void Play(string filePath)
    {
        Stop(); // Ensure previous resources are cleaned up

        var fileName = Path.GetFileName(filePath);
        if (BmsAtelierKyokufu.BmsPartTuner.Core.Audio.VirtualAudioRegistry.TryGetFile(fileName, out var memoryData))
        {
            _memoryStreamToDispose = new MemoryStream(memoryData);
            _audioReader = new WaveFileReader(_memoryStreamToDispose);
        }
        else
        {
            _audioReader = new AudioFileReader(filePath);
        }

        _waveOut = new WaveOutEvent();
        _waveOut.Init(_audioReader);
        _waveOut.PlaybackStopped += OnPlaybackStopped;
        _waveOut.Play();
    }

    public void Stop()
    {
        _waveOut?.Stop();
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Dispose();
            _waveOut = null;
        }

        _audioReader?.Dispose();
        _audioReader = null;

        _memoryStreamToDispose?.Dispose();
        _memoryStreamToDispose = null;
    }
}
