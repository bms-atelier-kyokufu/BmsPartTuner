namespace BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Audio;

/// <summary>
/// Factory interface for creating IAudioPlayer instances.
/// </summary>
public interface IAudioPlayerFactory
{
    /// <summary>
    /// Creates a new IAudioPlayer instance.
    /// </summary>
    /// <returns>A new IAudioPlayer.</returns>
    IAudioPlayer CreatePlayer();
}
