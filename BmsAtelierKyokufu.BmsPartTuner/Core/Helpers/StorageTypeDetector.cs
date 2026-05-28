using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;

/// <summary>
/// ドライブがSSD等のシークペナルティの無いストレージかどうかを判定するユーティリティ。
/// </summary>
internal static partial class StorageTypeDetector
{
    private static readonly ConcurrentDictionary<string, bool> _ssdCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 指定されたパスが含まれるドライブがSSDであるかどうかを判定します。
    /// 判定結果はドライブレターごとにキャッシュされます。
    /// </summary>
    public static bool IsSolidStateDrive(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            string root = Path.GetPathRoot(path) ?? string.Empty;
            if (string.IsNullOrEmpty(root)) return false;

            string driveLetter = root.TrimEnd('\\', '/');
            return _ssdCache.GetOrAdd(driveLetter, DetectIsSsd);
        }
        catch
        {
            return false;
        }
    }

    private static bool DetectIsSsd(string driveLetter)
    {
        string drivePath = $@"\\.\{driveLetter}";

        // READ/WRITEアクセス権なし(0)で開くことで、管理者権限がなくてもメタデータを取得可能
        using SafeFileHandle hDevice = CreateFile(
            drivePath,
            0, 
            FILE_SHARE_READ | FILE_SHARE_WRITE,
            IntPtr.Zero,
            OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (hDevice.IsInvalid)
        {
            return false; // 開けなかった場合は安全のため HDD (false) とみなす
        }

        var query = new STORAGE_PROPERTY_QUERY
        {
            PropertyId = StorageDeviceSeekPenaltyProperty,
            QueryType = PropertyStandardQuery,
            AdditionalParameters = 0
        };

        var result = new DEVICE_SEEK_PENALTY_DESCRIPTOR();

        bool success = DeviceIoControl(
            hDevice,
            IOCTL_STORAGE_QUERY_PROPERTY,
            ref query,
            (uint)Marshal.SizeOf<STORAGE_PROPERTY_QUERY>(),
            ref result,
            (uint)Marshal.SizeOf<DEVICE_SEEK_PENALTY_DESCRIPTOR>(),
            out _,
            IntPtr.Zero);

        if (success)
        {
            // IncursSeekPenaltyがfalseであれば、物理的なシーク遅延が発生しないSSDとみなせる
            return result.IncursSeekPenalty == 0;
        }

        return false;
    }

    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint OPEN_EXISTING = 3;
    private const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x2D1400;

    private const int StorageDeviceSeekPenaltyProperty = 7;
    private const int PropertyStandardQuery = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct STORAGE_PROPERTY_QUERY
    {
        public int PropertyId;
        public int QueryType;
        public byte AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVICE_SEEK_PENALTY_DESCRIPTOR
    {
        public int Version;
        public int Size;
        public byte IncursSeekPenalty;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFile(
        [MarshalAs(UnmanagedType.LPWStr)] string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        ref STORAGE_PROPERTY_QUERY lpInBuffer,
        uint nInBufferSize,
        ref DEVICE_SEEK_PENALTY_DESCRIPTOR lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);
}
