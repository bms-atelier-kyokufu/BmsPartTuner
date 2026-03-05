using NAudio.Wave;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.AudioPlayer;

/// <summary>
/// NAudio implementation of IAudioPlayer.
/// </summary>
public class NAudioPlayer : IAudioPlayer
{
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioFileReader;

    public event EventHandler? PlaybackStopped;

    public void Play(string filePath)
    {
        Stop(); // Ensure previous resources are cleaned up

        _audioFileReader = new AudioFileReader(filePath);
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_audioFileReader);
        _waveOut.PlaybackStopped += OnPlaybackStopped;
        _waveOut.Play();
    }

    public void Stop()
    {
        if (_waveOut != null)
        {
            _waveOut.Stop();
            // Dispose is called in Dispose() or when needed, but Stop usually triggers PlaybackStopped
        }
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_waveOut != null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Dispose();
            _waveOut = null;
        }

        if (_audioFileReader != null)
        {
            _audioFileReader.Dispose();
            _audioFileReader = null;
        }
    }
}
