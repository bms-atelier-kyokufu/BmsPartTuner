using NAudio.Wave;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Audio.AudioPlayer;

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
        _waveOut?.Stop();
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

        _audioFileReader?.Dispose();
        _audioFileReader = null;
    }
}
