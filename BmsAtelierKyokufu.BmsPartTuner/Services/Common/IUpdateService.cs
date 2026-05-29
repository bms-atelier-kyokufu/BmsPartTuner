using System;
using System.Threading.Tasks;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Common;

public interface IUpdateService : IDisposable
{
    bool IsUpdateReady { get; }
    Version? AvailableVersion { get; }
    Task CheckForUpdatesAsync();
    void LaunchUpdateInstaller();
}
