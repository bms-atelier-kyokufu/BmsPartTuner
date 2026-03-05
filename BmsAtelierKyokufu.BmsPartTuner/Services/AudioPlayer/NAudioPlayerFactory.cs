namespace BmsAtelierKyokufu.BmsPartTuner.Services.AudioPlayer;

public class NAudioPlayerFactory : IAudioPlayerFactory
{
    public IAudioPlayer CreatePlayer()
    {
        return new NAudioPlayer();
    }
}
