namespace BmsAtelierKyokufu.BmsPartTuner.Services.Audio.AudioPlayer;

public class NAudioPlayerFactory : IAudioPlayerFactory
{
    public IAudioPlayer CreatePlayer()
    {
        return new NAudioPlayer();
    }
}
