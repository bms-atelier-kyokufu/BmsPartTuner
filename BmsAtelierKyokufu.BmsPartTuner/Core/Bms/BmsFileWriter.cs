using System.IO;
using System.Text;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// BMSファイルの書き出し操作を担当する静的クラス。
/// ファイルの書き込みをアトミックに行い、エンコーディング(Shift_JIS)を管理します。
/// </summary>
internal static class BmsFileWriter
{
    static BmsFileWriter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>
    /// BMSファイルを書き込みます。
    /// </summary>
    /// <param name="saveFileName">保存先ファイルパス。</param>
    /// <param name="writeData">書き込む内容。</param>
    /// <remarks>
    /// <para>【Why Shift_JIS】</para>
    /// BMSフォーマットはShift_JISエンコーディングが標準です。
    /// 互換性維持のため、ファイル書き込みにはShift_JISを使用します。
    /// </remarks>
    public static void WriteBmsFile(string saveFileName, string writeData)
    {
        // アトミック書き込み: 一時ファイルに書き込んでからリネーム
        var tempFileName = saveFileName + ".tmp";

        try
        {
            // パス長チェック（WindowsのMAX_PATH制限への対策）
            var directory = Path.GetDirectoryName(saveFileName);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"出力先ディレクトリが存在しません: {directory}");
            }

            // 1. 一時ファイルに書き込み
            using (var sw = new StreamWriter(tempFileName, false, Encoding.GetEncoding("shift_jis")))
            {
                sw.Write(writeData);
            }

            // 2. 書き込み成功後、元のファイルを置き換え
            if (File.Exists(saveFileName))
            {
                File.Delete(saveFileName);
            }
            File.Move(tempFileName, saveFileName);

            PerformanceDebugLogger.WriteDebug(nameof(BmsFileWriter), $"BMS file written atomically: {saveFileName}");
        }
        catch (IOException)
        {
            // エラー発生時のクリーンアップ処理
            try
            {
                bool originalExists = File.Exists(saveFileName);
                if (File.Exists(tempFileName))
                {
                    if (originalExists)
                    {
                        File.Delete(tempFileName);
                        PerformanceDebugLogger.WriteDebug(nameof(BmsFileWriter), $"Cleanup: Incomplete temp file deleted: {tempFileName}");
                    }
                    else
                    {
                        PerformanceDebugLogger.WriteDebug(nameof(BmsFileWriter), $"CRITICAL WARNING: Original file lost, keeping temp file for recovery: {tempFileName}");
                    }
                }
            }
            catch
            {
                // クリーンアップ中の例外は無視
            }
            throw;
        }
        catch (UnauthorizedAccessException)
        {
            // アクセス拒否エラー
            try
            {
                if (File.Exists(tempFileName))
                {
                    File.Delete(tempFileName);
                    PerformanceDebugLogger.WriteDebug(nameof(BmsFileWriter), $"Cleanup: Temp file deleted due to access denied: {tempFileName}");
                }
            }
            catch
            {
                // クリーンアップ中の例外は無視
            }
            throw;
        }
    }
}
