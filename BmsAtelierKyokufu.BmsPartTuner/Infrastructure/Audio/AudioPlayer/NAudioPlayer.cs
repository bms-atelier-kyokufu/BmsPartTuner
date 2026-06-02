using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Audio;
using NAudio.Wave;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Audio.AudioPlayer;

/// <summary>
/// NAudio implementation of IAudioPlayer.
/// </summary>
public class NAudioPlayer : IAudioPlayer
{
    private bool _disposed;
    private WaveOutEvent? _waveOut;
    private WaveStream? _audioReader;
    private Stream? _memoryStreamToDispose;

    public event EventHandler? PlaybackStopped;

    public void Play(string filePath)
    {
        Stop(); // Ensure previous resources are cleaned up

        var fileName = Path.GetFileName(filePath);
        if (VirtualAudioRegistry.TryGetStream(fileName, out var stream))
        {
            _memoryStreamToDispose = stream;
            _audioReader = new WaveFileReader(_memoryStreamToDispose);
        }
        else
        {
            if (filePath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
            {
                _audioReader = new NAudio.Vorbis.VorbisWaveReader(filePath);
            }
            else
            {
                _audioReader = new AudioFileReader(filePath);
            }
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
        if (_disposed) return;
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
        _disposed = true;
    }
}
