namespace BmsAtelierKyokufu.BmsPartTuner.Core;

/// <summary>
/// アプリケーション全体で使用される定数を一元管理します。
/// マジックナンバーを排除し、パフォーマンスや環境設定のパラメータを集約してコードの意図を明確化します。
/// </summary>
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
        /// 置換テーブルのサイズ（62進数の最大値+1）。0-indexedで全定義番号を直接アクセス可能にするために+1しています。
        /// </summary>
        public const int ReplaceTableSize = MaxNumberBase62 + 1;

        /// <summary>デフォルトの定義範囲開始値（文字列）。</summary>
        public const string Start = "01";

        /// <summary>
        /// デフォルトの定義範囲終了値（文字列、"00"は特別な値で、自動的に最大定義番号を検出することを示します）。
        /// </summary>
        public const string End = "00";

        /// <summary>
        /// BMSデータ中の休符/未定義オブジェクトプレースホルダー。
        /// </summary>
        public const string Rest = "00";

        /// <summary>WAV定義コマンド。</summary>
        public const string Wav = "WAV";

        /// <summary>BPM定義コマンド。</summary>
        public const string Bpm = "BPM";

        /// <summary>STOP定義コマンド。</summary>
        public const string Stop = "STOP";

        /// <summary>BMP定義コマンド。</summary>
        public const string Bmp = "BMP";

        /// <summary>WAV定義コマンドのプレフィックス。</summary>
        public const string WavPrefix = "#WAV";

        /// <summary>BPM定義コマンドのプレフィックス。</summary>
        public const string BpmPrefix = "#BPM";

        /// <summary>STOP定義コマンドのプレフィックス。</summary>
        public const string StopPrefix = "#STOP";

        /// <summary>BMP定義コマンドのプレフィックス。</summary>
        public const string BmpPrefix = "#BMP";

        /// <summary>定義文字列の必須長（2桁固定）。</summary>
        public const int StringLength = 2;

        /// <summary>36進数の基数。</summary>
        public const int RadixBase36 = 36;

        /// <summary>62進数の基数。</summary>
        public const int RadixBase62 = 62;
    }

    /// <summary>
    /// BMSのTOTAL値の自動計算（bmsonからの変換等）に関する定数。
    /// </summary>
    public static class BmsTotal
    {
        /// <summary>
        /// 本家IIDXのNORMALゲージ回復量に基づいた近似式（black train式）の係数。
        /// </summary>
        public const double IidxMultiplier = 7.605;

        /// <summary>
        /// black train式の分母の一次係数（総ノーツ数に対する倍率）。
        /// </summary>
        public const double IidxNotesCoefficient = 0.01;

        /// <summary>
        /// black train式の分母の定数項。
        /// </summary>
        public const double IidxConstantTerm = 6.5;

        /// <summary>
        /// 低ノーツ数曲において、ゲームバランス（クリア可能性）を担保するための最低保証TOTAL値。
        /// </summary>
        public const double MinimumFloor = 260.0;

        /// <summary>
        /// bmsonにおけるデフォルトのtotal割合値 (100.0%)。
        /// </summary>
        public const double DefaultPercentage = 100.0;
    }

    /// <summary>
    /// 音声フォーマットに関する定数。
    /// </summary>
    public static class Audio
    {
        /// <summary>標準オーディオサンプルレート (44100 Hz)。</summary>
        public const int StandardSampleRate = 44100;
    }

    /// <summary>
    /// 音声比較処理に関する定数。
    /// </summary>
    public static class AudioComparison
    {
        /// <summary>
        /// サンプル長の類似性判定における許容誤差（±10%）。
        /// 標準設定として末尾の無音の違いを許容します。
        /// </summary>
        public const float LengthSimilarityTolerance = 0.1f;

        /// <summary>
        /// RMS（音圧）の類似性判定における許容誤差（±20%）。
        /// Phase 3フィルタで使用され、全比較の約85%をここで除外する標準的な設定です。
        /// </summary>
        public const float RmsSimilarityThreshold = 0.2f;

        /// <summary>
        /// RMS類似性判定の下限倍率（-20%、1.0 - 0.2 = 0.8）。
        /// </summary>
        public const float RmsLowerBoundRatio = 0.8f;

        /// <summary>
        /// RMS類似性判定の上限倍率（+25%、1 / 0.8 = 1.25）。
        /// </summary>
        public const float RmsUpperBoundRatio = 1.25f;

        /// <summary>
        /// 早期終了チェックで使用するサンプル数（44.1kHz × 0.1秒）。
        /// 全サンプル比較の前に、冒頭部分のみで高速判定を行うことで処理時間を短縮します。
        /// </summary>
        public const int QuickCheckSampleCount = 4410;

        /// <summary>
        /// 無音判定のRMS閾値（この値以下のRMSを持つ音声は無音として扱われます）。
        /// </summary>
        public const float SilenceRmsThreshold = 0.000001f;

        /// <summary>
        /// 無音ファイルのRMS上限閾値。
        /// SilenceRmsThresholdの2倍の値とし、無音判定の安全マージンとして使用します。
        /// </summary>
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
        /// 並列化効率とメモリ使用量のバランスを取るための標準設定（約20MB/100ファイル）です。
        /// </summary>
        public const int MaxGroupSize = 100;

        /// <summary>
        /// RMS量子化係数（0.01刻み = 100）。
        /// RMS値を整数化してグループキーを生成するために使用します（例: RMS=0.456 → int(0.456 × 100) = 45）。
        /// </summary>
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
        /// バッチ分割の除数。
        /// CPUコア数にこの値を乗じた数にバッチを分割します（例: 8コア × 4 = 32バッチに分割）。
        /// </summary>
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
        public static readonly string[] SupportedBmsExtensions = [".bms", ".bme", ".bml", ".pms", ".bmson"];

        /// <summary>出力対応するBMSファイルの拡張子一覧（bmsonを除く）。</summary>
        public static readonly string[] SupportedOutputBmsExtensions = [".bms", ".bme", ".bml", ".pms"];

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
        /// UI表示時に「BMSファイルを選択」等の適切なメッセージを生成するために使用します。
        /// </summary>
        /// <param name="extension">ファイル拡張子（.bms など）。</param>
        /// <returns>種類名（"BMSファイル" など）、不明な場合は "ファイル"。</returns>
        public static string GetFileTypeName(string extension)
        {
            return extension?.ToLower() switch
            {
                ".bms" => "BMSファイル",
                ".bme" => "BMEファイル",
                ".bml" => "BMLファイル",
                ".pms" => "PMSファイル",
                ".bmson" => "BMSONファイル",
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
        /// 処理が高速に完了する場合のローダーチラつき防止用に使用します。
        /// </summary>
        public const int LoaderDelayMs = 500;

        /// <summary>
        /// 音声プレビューのデバウンス遅延時間（ミリ秒）。
        /// 連続クリック時に最後の選択のみを再生するための遅延として機能します。
        /// </summary>
        public const int AudioPreviewDelayMs = 300;
    }

    /// <summary>
    /// ログ設定に関する定数。
    /// </summary>
    public static class Logging
    {
        /// <summary>デフォルトの最小出力ログレベル（DEBUGビルド時のみ機能）</summary>
        public const LogLevel DefaultLogLevel = LogLevel.Trace;
    }
}
