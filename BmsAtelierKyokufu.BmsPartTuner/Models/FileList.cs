using System.Collections.ObjectModel;
using System.Diagnostics;
using BmsAtelierKyokufu.BmsPartTuner.Core;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;
using BmsAtelierKyokufu.BmsPartTuner.Services;

namespace BmsAtelierKyokufu.BmsPartTuner.Models;

/// <summary>
/// BMSファイルに関連付けられたオーディオファイルリストの管理および解析を行います。
/// </summary>
/// <remarks>
/// <para>【責務】</para>
/// <list type="number">
/// <item>BMSファイルから#WAV定義を解析</item>
/// <item>参照されているオーディオファイルの存在確認</item>
/// <item>ファイルメタデータ（サイズ、定義番号）の取得</item>
/// <item>楽器名の統計的推定（<see cref="InstrumentNameDetectionService"/>連携）</item>
/// </list>
/// 
/// <para>【Why ObservableCollection】</para>
/// WPFのListBoxやDataGridにバインドされるため、コレクション変更を自動的にUIに反映させる必要があります。
/// </remarks>
public class FileList
{
    /// <summary>
    /// WAVファイル情報（1ファイル分のメタデータ）。
    /// </summary>
    /// <remarks>
    /// <para>【データ構造】</para>
    /// <list type="bullet">
    /// <item>Num: ZZ進数表記（例: "01", "0Z", "ZZ"）</item>
    /// <item>NumInteger: 10進数表記（例: 1, 35, 1295）</item>
    /// <item>Name: ファイルフルパス</item>
    /// <item>FileSize: バイト単位のサイズ</item>
    /// <item>CachedData: メモリ上の音声データ（遅延ロード）</item>
    /// <item>InstrumentName: 推定された楽器種別（例: "kick", "snare"）</item>
    /// </list>
    /// </remarks>
    public class WavFiles
    {
        /// <summary>定義番号（ZZ進数、例: "01", "0Z"）。</summary>
        public string Num { get; set; } = string.Empty;

        /// <summary>定義番号（10進数、例: 1, 35）。</summary>
        public int NumInteger { get; set; }

        /// <summary>ファイルフルパス。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>ファイルサイズ（バイト）。</summary>
        public long FileSize { get; set; }

        /// <summary>
        /// キャッシュされた音声データ（遅延ロード）。
        /// </summary>
        /// <remarks>
        /// Why nullable: 必要になるまでメモリを消費しないため。
        /// </remarks>
        public CachedSoundData? CachedData { get; set; }

        /// <summary>オーディオフィンガープリント（将来の拡張用）。</summary>
        public string AudioFingerprint { get; set; } = string.Empty;

        /// <summary>
        /// 推定された楽器名（例: "kick", "snare", "hihat"）。
        /// 空文字列の場合は推定できなかったことを示します。
        /// </summary>
        public string InstrumentName { get; set; } = string.Empty;

        /// <summary>音声データをキャッシュにプリロードします（正規化なし）。</summary>
        public void PreloadCache() => PreloadCache(Models.NormalizationMode.None);

        /// <summary>
        /// 音声データをキャッシュにプリロードします。
        /// </summary>
        /// <remarks>
        /// <para>【Why 遅延ロード】</para>
        /// 全ファイルを一度にロードするとメモリ不足になる可能性があるため、
        /// 必要に応じて個別にロードします。
        /// 
        /// <para>【Why 正規化モード】</para>
        /// 音量差が大きいファイル群を比較する場合、波形を正規化することで
        /// 音量の影響を排除し、波形の形状のみを比較できます。
        /// </remarks>
        public void PreloadCache(Models.NormalizationMode normalizationMode)
        {
            if (CachedData != null)
                return;

            if (!File.Exists(Name))
            {
                Debug.WriteLine($"[WavFiles.PreloadCache] ERROR: File not found: {Path.GetFileName(Name)}");
                return;
            }

            try
            {
                CachedData = new CachedSoundData(Name, normalizationMode);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WavFiles.PreloadCache] ERROR loading {Path.GetFileName(Name)}: {ex.Message}");
            }
        }

        /// <summary>
        /// キャッシュされた音声データをクリアしてメモリを解放します。
        /// </summary>
        /// <remarks>
        /// <para>【用途】</para>
        /// 処理完了後に呼び出すことで、大量のメモリを即座に解放します。
        /// GC待ちよりも積極的にメモリを解放できます。
        /// 
        /// <para>【タイミング】</para>
        /// <list type="bullet">
        /// <item>シミュレーション完了後</item>
        /// <item>定義削減処理完了後</item>
        /// <item>エラー発生時のクリーンアップ</item>
        /// </list>
        /// </remarks>
        public void ClearCache()
        {
            CachedData = null;
        }
    }

    private readonly string _bmsFilePath;
    private string _bmsDirectory = string.Empty;
    private ObservableCollection<WavFiles> _fileList = new();
    private readonly InstrumentNameDetectionService _instrumentDetectionService;

    /// <summary>
    /// 見つからなかったファイルパスを記録するリスト。
    /// </summary>
    /// <remarks>
    /// <para>【用途】</para>
    /// ユーザーに欠落ファイルを通知し、BMSファイルの修正を促します。
    /// 例: "kick_01.wav が見つかりません"
    /// </remarks>
    public List<string> MissingFiles { get; private set; } = new();

    /// <summary>
    /// FileListを初期化します。
    /// </summary>
    /// <remarks>
    /// <para>【Why InstrumentNameDetectionServiceを内部生成】</para>
    /// FileListは単一のBMSファイルを管理するため、
    /// 楽器検出サービスのライフサイクルもFileListに合わせます。
    /// </remarks>
    public FileList(string bmsFilePath)
    {
        _bmsFilePath = bmsFilePath ?? throw new ArgumentNullException(nameof(bmsFilePath));
        _bmsDirectory = Path.GetDirectoryName(bmsFilePath) ?? string.Empty;
        _instrumentDetectionService = new InstrumentNameDetectionService();
    }

    public string GetBmsDirectory() => _bmsDirectory;

    public ObservableCollection<WavFiles> GetFileList() => _fileList;

    /// <summary>
    /// 全ファイルのキャッシュをクリアしてメモリを解放します。
    /// </summary>
    /// <remarks>
    /// <para>【用途】</para>
    /// 処理完了後に呼び出すことで、大量のメモリ（数百MB～数GB）を即座に解放します。
    /// 
    /// <para>【タイミング】</para>
    /// <list type="bullet">
    /// <item>最適化シミュレーション完了後</item>
    /// <item>定義削減処理完了後</item>
    /// <item>エラー発生時のクリーンアップ</item>
    /// </list>
    /// 
    /// <para>【効果】</para>
    /// GC待ちせずに即座にメモリを解放し、メモリリークを防止します。
    /// </remarks>
    public void ClearAllCaches()
    {
        Debug.WriteLine($"=== ClearAllCaches Start: {_fileList.Count} files ===");

        int clearedCount = 0;
        double freedMemoryMB = 0;

        foreach (var file in _fileList)
        {
            if (file.CachedData != null)
            {
                freedMemoryMB += file.CachedData.EstimatedMemoryMB;
                file.CachedData.Dispose();
                file.ClearCache();
                clearedCount++;
            }
        }

        Debug.WriteLine($"=== ClearAllCaches Complete: {clearedCount} caches cleared, {freedMemoryMB:F2} MB freed ===");

        // GCを強制実行してメモリを確実に解放（LOH含む）
        Debug.WriteLine("=== Forcing aggressive GC ===");
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

        long memoryAfterGC = GC.GetTotalMemory(forceFullCollection: true);
        Debug.WriteLine($"=== Memory after GC: {memoryAfterGC / 1024.0 / 1024.0:F2} MB ===");
    }

    /// <summary>
    /// BMSファイルからファイルリストを作成します。
    /// </summary>
    /// <remarks>
    /// <para>【処理フロー】</para>
    /// <list type="number">
    /// <item>BMSファイルから#WAV定義を解析</item>
    /// <item>基数（36進 or 62進）を自動判定</item>
    /// <item>ファイル存在確認とメタデータ取得</item>
    /// <item>楽器名を統計的に推定</item>
    /// <item>ObservableCollectionに追加（UI反映）</item>
    /// </list>
    /// 
    /// <para>【Why 一時リストを使用】</para>
    /// <see cref="ObservableCollection{T}"/>への頻繁なAddは、毎回CollectionChangedイベントを
    /// 発火させUIを更新するため、パフォーマンスが低下します。
    /// 一時リストで処理してから一括追加することで、UI更新回数を削減します。
    /// 
    /// <para>【Why 基数を自動判定】</para>
    /// BMSフォーマットは36進数（0-9,A-Z）が標準ですが、
    /// 拡張仕様で62進数（0-9,A-Z,a-z）も使用されます。
    /// 定義に小文字が含まれていれば62進数と判定します。
    /// </remarks>
    public ObservableCollection<WavFiles> CreateFileList()
    {
        MissingFiles.Clear();

        var manager = new BmsManager(_bmsFilePath);
        var definitions = manager.ParseWavDefinitions();

        bool isBase62 = definitions.Any(d => System.Text.RegularExpressions.Regex.IsMatch(d.def, "[a-z]"));
        int inputRadix = isBase62 ? AppConstants.Definition.RadixBase62 : AppConstants.Definition.RadixBase36;

        var tempList = new List<WavFiles>();

        foreach (var (def, path) in definitions)
        {
            var fullPath = Path.IsPathRooted(path)
                ? path
                : Path.Combine(_bmsDirectory, path);

            if (!File.Exists(fullPath))
            {
                Debug.WriteLine($"[FileList] Missing file: {path}");
                MissingFiles.Add(path);
                continue;
            }

            var fileInfo = new FileInfo(fullPath);

            tempList.Add(new WavFiles
            {
                Num = def,
                NumInteger = RadixConvert.ZZToInt(def, inputRadix),
                Name = fullPath,
                FileSize = fileInfo.Length,
                AudioFingerprint = string.Empty,
                InstrumentName = string.Empty
            });
        }

        AssignInstrumentNames(tempList);

        foreach (var file in tempList)
        {
            _fileList.Add(file);
        }

        return _fileList;
    }

    /// <summary>
    /// ファイル名の統計分析に基づいて楽器名を推定・設定します。
    /// </summary>
    /// <remarks>
    /// <para>【Why try-catch】</para>
    /// 楽器名推定はオプショナルな機能であり、失敗してもファイルリスト作成は
    /// 継続すべきです。エラーが発生しても、InstrumentNameを空文字列のままにして処理を続行します。
    /// 
    /// <para>【処理タイミング】</para>
    /// ObservableCollectionに追加する前に実行することで、
    /// UIへの通知回数を削減（InstrumentName設定による追加通知を避ける）します。
    /// </remarks>
    private void AssignInstrumentNames(List<WavFiles> files)
    {
        try
        {
            var detectionResult = _instrumentDetectionService.DetectInstruments(files);

            foreach (var file in files)
            {
                if (detectionResult.FileInstrumentMap.TryGetValue(file.Name, out string? instrumentName))
                {
                    file.InstrumentName = instrumentName;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[FileList.AssignInstrumentNames] ERROR: {ex.Message}");
        }
    }
}
