namespace BmsAtelierKyokufu.BmsPartTuner.Services.AudioPlayer;

/// <summary>
/// Audio player interface for abstraction and testing.
/// </summary>
public interface IAudioPlayer : IDisposable
{
    /// <summary>
    /// Occurs when playback is stopped.
    /// </summary>
    event EventHandler PlaybackStopped;

    /// <summary>
    /// Initializes and plays the specified audio file.
    /// </summary>
    /// <param name="filePath">The path to the audio file.</param>
    void Play(string filePath);

    /// <summary>
    /// Stops playback.
    /// </summary>
    void Stop();
}
