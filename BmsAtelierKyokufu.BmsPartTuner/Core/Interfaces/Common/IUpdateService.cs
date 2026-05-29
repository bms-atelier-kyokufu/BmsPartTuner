namespace BmsAtelierKyokufu.BmsPartTuner.Core.Interfaces.Common;

public interface IUpdateService : IDisposable
{
    bool IsUpdateReady { get; }
    Version? AvailableVersion { get; }
    Task CheckForUpdatesAsync();
    void LaunchUpdateInstaller();
}
