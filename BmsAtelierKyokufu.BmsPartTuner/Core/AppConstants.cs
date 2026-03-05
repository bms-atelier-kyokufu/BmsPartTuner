namespace BmsAtelierKyokufu.BmsPartTuner.Core;

/// <summary>
/// アプリケーション全体で使用される定数を一元管理します。
/// </summary>
/// <remarks>
/// <para>【設計思想】</para>
/// <list type="bullet">
/// <item>マジックナンバーを排除し、コードの意図を明確化</item>
/// <item>パフォーマンスチューニングのパラメータを集約</item>
/// <item>環境に応じた調整を容易にする</item>
/// </list>
/// 
/// <para>【調整ガイド】</para>
/// 各定数には、環境（メモリ量、CPU性能）に応じた調整指針を記載しています。
/// デフォルト値は一般的な環境（8コア、16GB RAM）を想定しています。
/// </remarks>
public static class AppConstants
{
    /// <summary>
    /// BMS定義番号に関する定数。
    /// </summary>
    public static class Definition
    {
        /// <summary>定義番号の最小値（BMSフォーマット仕様: 1から開始）。</summary>
        public const int MinNumber = 1;

        /// <summary>
        /// 36進数（0-9, A-Z）での最大定義番号 "ZZ"（計算: 36^2 - 1 = 1295）。
        /// </summary>
        public const int MaxNumberBase36 = 1295;

        /// <summary>
        /// 62進数（0-9, A-Z, a-z）での最大定義番号 "zz"（計算: 62^2 - 1 = 3843、拡張BMS仕様で使用）。
        /// </summary>
        public const int MaxNumberBase62 = 3843;

        /// <summary>
        /// 置換テーブルのサイズ（62進数の最大値+1）。
        /// </summary>
        /// <remarks>
        /// Why +1: 0-indexedで全定義番号を直接アクセス可能にするため。
        /// </remarks>
        public const int ReplaceTableSize = MaxNumberBase62 + 1;

        /// <summary>デフォルトの定義範囲開始値（文字列）。</summary>
        public const string Start = "01";

        /// <summary>
        /// デフォルトの定義範囲終了値（文字列、0=自動検出）。
        /// </summary>
        /// <remarks>
        /// "00"は特別な値で、自動的に最大定義番号を検出することを示します。
        /// </remarks>
        public const string End = "00";

        /// <summary>定義文字列の必須長（2桁固定）。</summary>
        public const int StringLength = 2;

        /// <summary>36進数の基数。</summary>
        public const int RadixBase36 = 36;

        /// <summary>62進数の基数。</summary>
        public const int RadixBase62 = 62;
    }

    /// <summary>
    /// 音声比較処理に関する定数。
    /// </summary>
    public static class AudioComparison
    {
        /// <summary>
        /// サンプル長の類似性判定における許容誤差（±10%）。
        /// </summary>
        /// <remarks>
        /// <para>【調整ガイド】</para>
        /// <list type="bullet">
        /// <item>厳密: 0.05f（±5%） - サンプル数がほぼ一致する場合のみ</item>
        /// <item>標準: 0.1f（±10%） - 推奨、末尾無音の違いを許容</item>
        /// <item>緩い: 0.15f（±15%） - フェードアウトの差を許容</item>
        /// </list>
        /// </remarks>
        public const float LengthSimilarityTolerance = 0.1f;

        /// <summary>
        /// RMS（音圧）の類似性判定における許容誤差（±20%）。
        /// </summary>
        /// <remarks>
        /// <para>【重要】</para>
        /// Phase 3フィルタで使用。全比較の約85%をここで除外します。
        /// 
        /// <para>【調整ガイド】</para>
        /// <list type="bullet">
        /// <item>厳密: 0.1f（±10%） - 音量がほぼ同じファイルのみ</item>
        /// <item>標準: 0.2f（±20%） - 推奨、正規化された音源に適合</item>
        /// <item>緩い: 0.3f（±30%） - 音量差が大きいライブラリ向け</item>
        /// </list>
        /// </remarks>
        public const float RmsSimilarityThreshold = 0.2f;

        /// <summary>
        /// RMS類似性判定の下限倍率（-20%）。
        /// </summary>
        /// <remarks>
        /// RmsSimilarityThresholdから計算: 1.0 - 0.2 = 0.8
        /// </remarks>
        public const float RmsLowerBoundRatio = 0.8f;

        /// <summary>
        /// RMS類似性判定の上限倍率（+25%）。
        /// </summary>
        /// <remarks>
        /// RmsLowerBoundRatioの逆数: 1 / 0.8 = 1.25
        /// </remarks>
        public const float RmsUpperBoundRatio = 1.25f;

        /// <summary>
        /// 早期終了チェックで使用するサンプル数（44.1kHz × 0.1秒）。
        /// </summary>
        /// <remarks>
        /// <para>【目的】</para>
        /// 全サンプル比較の前に、冒頭部分のみで高速判定。
        /// Phase 4フィルタで約6%をここで除外します。
        /// 
        /// <para>【調整ガイド】</para>
        /// <list type="bullet">
        /// <item>高速: 2205（0.05秒） - 処理速度優先</item>
        /// <item>標準: 4410（0.1秒） - 推奨、精度とのバランス</item>
        /// <item>高精度: 8820（0.2秒） - 精度優先、処理時間増</item>
        /// </list>
        /// </remarks>
        public const int QuickCheckSampleCount = 4410;

        /// <summary>
        /// 無音判定のRMS閾値（この値以下のRMSを持つ音声は無音として扱われます）。
        /// </summary>
        public const float SilenceRmsThreshold = 0.001f;

        /// <summary>
        /// 無音ファイルのRMS上限閾値。
        /// </summary>
        /// <remarks>
        /// SilenceRmsThresholdの2倍の値。無音判定の安全マージンとして使用。
        /// </remarks>
        public const float SilenceRmsUpperBound = 0.002f;
    }

    /// <summary>
    /// しきい値に関する定数。
    /// </summary>
    public static class Threshold
    {
        /// <summary>デフォルトのしきい値（標準設定）。</summary>
        public const float Default = 0.4f;

        /// <summary>
        /// しきい値の推奨最小値（これより低い値は、波形が全く似ていないことを示します）。
        /// </summary>
        public const float Min = 0.70f;

        /// <summary>しきい値の最大値（完全一致）。</summary>
        public const float Max = 1.0f;

        /// <summary>
        /// しきい値の検証用最小値（入力可能な最小値）。
        /// </summary>
        public const float MinValueForValidation = 0.0f;

        /// <summary>
        /// しきい値の表示用最小値（1-100スケール）
        /// </summary>
        public const int MinDisplay = 0;

        /// <summary>
        /// しきい値の表示用最大値（1-100スケール）
        /// </summary>
        public const int MaxDisplay = 100;

        /// <summary>
        /// しきい値の表示用デフォルト値（1-100スケール）
        /// </summary>
        public const int DefaultDisplay = 40;
    }

    /// <summary>
    /// ファイルグループ化に関する定数。
    /// </summary>
    public static class Grouping
    {
        /// <summary>
        /// グループの最大サイズ（ファイル数）。
        /// </summary>
        /// <remarks>
        /// <para>【目的】</para>
        /// 並列化効率とメモリ使用量のバランスを取ります。
        /// 
        /// <para>【Why 100】</para>
        /// <list type="bullet">
        /// <item>100ファイルの全ペア比較: 4,950回（許容範囲）</item>
        /// <item>メモリ: 100ファイル × 200KB ≈ 20MB（許容範囲）</item>
        /// </list>
        /// 
        /// <para>【調整ガイド】</para>
        /// <list type="bullet">
        /// <item>大規模環境（メモリ豊富）: 50-75</item>
        /// <item>標準環境: 100（推奨）</item>
        /// <item>小規模環境（メモリ制約）: 150-200</item>
        /// </list>
        /// </remarks>
        public const int MaxGroupSize = 100;

        /// <summary>
        /// RMS量子化係数（0.01刻み = 100）。
        /// </summary>
        /// <remarks>
        /// <para>【目的】</para>
        /// RMS値を整数化してグループキーを生成。
        /// 例: RMS=0.456 → int(0.456 × 100) = 45
        /// 
        /// <para>【調整ガイド】</para>
        /// <list type="bullet">
        /// <item>粗い分割: 50（0.02刻み） - グループ数減、比較回数増</item>
        /// <item>標準: 100（0.01刻み） - 推奨</item>
        /// <item>細かい分割: 200（0.005刻み） - グループ数増、比較回数減</item>
        /// </list>
        /// </remarks>
        public const int RmsQuantizationFactor = 100;
    }

    /// <summary>
    /// キャッシュ管理に関する定数。
    /// </summary>
    public static class Cache
    {
        /// <summary>
        /// バッチ処理の最小サイズ（プリロード時のバッチサイズ下限値）。
        /// </summary>
        public const int MinBatchSize = 10;

        /// <summary>
        /// バッチ分割の除数（CPUコア数 × この値）。
        /// </summary>
        /// <remarks>
        /// <para>【計算例】</para>
        /// 8コア × 4 = 32バッチに分割
        /// 
        /// <para>【調整ガイド】</para>
        /// <list type="bullet">
        /// <item>メモリ豊富: 2（大きなバッチ、ロードオーバーヘッド削減）</item>
        /// <item>標準: 4（推奨）</item>
        /// <item>メモリ制約: 8（小さなバッチ、メモリピーク削減）</item>
        /// </list>
        /// </remarks>
        public const int BatchSizeDivisor = 4;
    }

    /// <summary>
    /// 進捗報告の閾値に関する定数。
    /// </summary>
    public static class Progress
    {
        /// <summary>プリロード完了時の進捗値（%）。</summary>
        public const int PreloadComplete = 10;

        /// <summary>比較処理完了時の進捗値（%）。</summary>
        public const int ComparisonComplete = 80;

        /// <summary>BMS書き換え完了時の進捗値（%）。</summary>
        public const int RewriteComplete = 90;

        /// <summary>全処理完了時の進捗値（%）。</summary>
        public const int Complete = 100;
    }

    /// <summary>
    /// ファイル操作に関する定数。
    /// </summary>
    public static class Files
    {
        /// <summary>対応するBMSファイルの拡張子一覧。</summary>
        public static readonly string[] SupportedBmsExtensions = { ".bms", ".bme", ".bml", ".pms" };

        /// <summary>デフォルトの出力ファイル名。</summary>
        public const string DefaultOutputFileName = "output.bms";

        /// <summary>GitHubリポジトリのURL。</summary>
        public const string GitHubRepositoryUrl = "https://github.com/bms-atelier-kyokufu/BmsPartTuner";

        /// <summary>
        /// 最適化後のファイル名に付与するサフィックス（例: "song.bms" → "song_optimized.bms"）。
        /// </summary>
        public const string OptimizedFileSuffix = "_optimized";

        /// <summary>
        /// ファイル拡張子から対応する種類名を取得します。
        /// </summary>
        /// <param name="extension">ファイル拡張子（.bms など）。</param>
        /// <returns>種類名（"BMSファイル" など）、不明な場合は "ファイル"。</returns>
        /// <remarks>
        /// <para>【用途】</para>
        /// UI表示時に「BMSファイルを選択」等の適切なメッセージを生成します。
        /// </remarks>
        public static string GetFileTypeName(string extension)
        {
            return extension?.ToLower() switch
            {
                ".bms" => "BMSファイル",
                ".bme" => "BMEファイル",
                ".bml" => "BMLファイル",
                ".pms" => "PMSファイル",
                _ => "ファイル"
            };
        }
    }

    /// <summary>
    /// UI動作に関する定数。
    /// </summary>
    public static class UI
    {
        /// <summary>
        /// バーチャルスライダーの動作設定。
        /// </summary>
        public static class VirtualSlider
        {
            /// <summary>
            /// 整数モード時に1ステップ進むために必要なピクセル数（通常速度）。
            /// </summary>
            public const double IntegerPixelsPerStepNormal = 8.0;

            /// <summary>
            /// 整数モード時に1ステップ進むために必要なピクセル数（高速: Shift）。
            /// </summary>
            public const double IntegerPixelsPerStepFast = 3.0;

            /// <summary>
            /// 整数モード時に1ステップ進むために必要なピクセル数（微調整: Ctrl）。
            /// </summary>
            public const double IntegerPixelsPerStepFine = 20.0;

            /// <summary>
            /// 小数モード時の乗数（通常速度）。
            /// </summary>
            public const double DecimalMultiplierNormal = 0.8;

            /// <summary>
            /// 小数モード時の乗数（高速: Shift）。
            /// </summary>
            public const double DecimalMultiplierFast = 2.0;

            /// <summary>
            /// 小数モード時の乗数（微調整: Ctrl）。
            /// </summary>
            public const double DecimalMultiplierFine = 0.3;

            /// <summary>
            /// ドラッグ開始と判定する最小移動ピクセル数。
            /// </summary>
            public const double DragThreshold = 2.0;
        }

        /// <summary>
        /// トースト通知の表示時間（ミリ秒）。
        /// </summary>
        public const int ToastDisplayDurationMs = 4000;

        /// <summary>
        /// プログレスローダーの遅延表示時間（ミリ秒）。
        /// </summary>
        /// <remarks>
        /// 処理が高速に完了する場合のローダーチラつき防止用。
        /// </remarks>
        public const int LoaderDelayMs = 500;

        /// <summary>
        /// 音声プレビューのデバウンス遅延時間（ミリ秒）。
        /// </summary>
        /// <remarks>
        /// 連続クリック時に最後の選択のみを再生するための遅延。
        /// </remarks>
        public const int AudioPreviewDelayMs = 300;
    }
}
