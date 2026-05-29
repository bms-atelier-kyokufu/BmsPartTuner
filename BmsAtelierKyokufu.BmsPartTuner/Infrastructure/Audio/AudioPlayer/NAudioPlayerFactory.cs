using BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Audio;
namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Audio.AudioPlayer;

public class NAudioPlayerFactory : IAudioPlayerFactory
{
    public IAudioPlayer CreatePlayer()
    {
        return new NAudioPlayer();
    }
}
