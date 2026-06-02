namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;

/// <summary>
/// BmsonのデータモデルとスライスされたWAVから、BMSファイルのテキストを生成するジェネレータ。
/// </summary>
[ADRAnchor("M-07", nameof(BmsScoreGenerator))]
public class BmsScoreGenerator(
    BmsonFormat bmson,
    PulseToBmsTimeCalculator timeCalc,
    PulseToRealTimeCalculator realTimeCalc,
    AudioSliceManager audioSliceManager,
    bool keyNotesOnly = false)
{
    // 内部定数の隠匿
    private const int RadixBase36 = 36;
    private const int RadixBase62 = 62;
    private const int MaxNumberBase36 = 1295;
    private const string RestValue = "00";

    private const double DefaultPercentage = 100.0;
    private const double MinimumFloor = 260.0;
    private const double IidxMultiplier = 7.605;
    private const double IidxNotesCoefficient = 0.01;
    private const double IidxConstantTerm = 6.5;

    /// <summary>
    /// BMSヘッダーに関するコマンド名とフォーマットロジックをカプセル化する構造体。
    /// </summary>
    private readonly struct BmsHeader
    {
        /// <summary>プレイヤー数定義コマンド (#PLAYER)。</summary>
        public const string Player = "#PLAYER";
        /// <summary>ジャンル定義コマンド (#GENRE)。</summary>
        public const string Genre = "#GENRE";
        /// <summary>タイトル定義コマンド (#TITLE)。</summary>
        public const string Title = "#TITLE";
        /// <summary>アーティスト定義コマンド (#ARTIST)。</summary>
        public const string Artist = "#ARTIST";
        /// <summary>サブアーティスト定義コマンド (#SUBARTIST)。</summary>
        public const string Subartist = "#SUBARTIST";
        /// <summary>難易度レベル定義コマンド (#PLAYLEVEL)。</summary>
        public const string PlayLevel = "#PLAYLEVEL";
        /// <summary>判定ランク定義コマンド (#RANK)。</summary>
        public const string Rank = "#RANK";
        /// <summary>ゲージ回復量定義コマンド (#TOTAL)。</summary>
        public const string Total = "#TOTAL";
        /// <summary>ロングノート種別定義コマンド (#LNTYPE)。</summary>
        public const string LnType = "#LNTYPE";

        /// <summary>BPM定義コマンドのプレフィックス (#BPM)。</summary>
        public const string BpmPrefix = "#BPM";
        /// <summary>WAV定義コマンドのプレフィックス (#WAV)。</summary>
        public const string WavPrefix = "#WAV";
        /// <summary>BMP定義コマンドのプレフィックス (#BMP)。</summary>
        public const string BmpPrefix = "#BMP";
        /// <summary>STOP定義コマンドのプレフィックス (#STOP)。</summary>
        public const string StopPrefix = "#STOP";

        /// <summary>
        /// 指定されたヘッダーコマンド名と値から、標準的なBMSヘッダー行文字列を生成します。
        /// </summary>
        /// <param name="name">ヘッダーコマンド名。</param>
        /// <param name="value">ヘッダーに設定する値。</param>
        /// <returns>"コマンド名 値" 形式のフォーマット文字列。</returns>
        public static string Format(string name, object? value) => $"{name} {value}";

        /// <summary>
        /// 指定された定義コマンドプレフィックス、インデックス、および値から、インデックス付きBMS定義行文字列を生成します。
        /// </summary>
        /// <param name="prefix">定義コマンドプレフィックス。</param>
        /// <param name="index">インデックス定義文字列（36進数や62進数など）。</param>
        /// <param name="value">定義対象のリソース値（ファイル名や値など）。</param>
        /// <returns>"プレフィックスインデックス 値" 形式のフォーマット文字列。</returns>
        public static string FormatIndexed(string prefix, string index, object? value) => $"{prefix}{index} {value}";
    }

    /// <summary>
    /// BMSチャンネル番号（レーンおよび制御イベント用）の定義を管理する構造体。
    /// </summary>
    private readonly struct BmsChannel
    {
        /// <summary>BGM音源用チャンネル (01)。</summary>
        public const string Bgm = "01";
        /// <summary>小節長変更用チャンネル (02)。</summary>
        public const string Meter = "02";
        /// <summary>BGAベース映像用チャンネル (04)。</summary>
        public const string Bga = "04";
        /// <summary>ミス画像/映像（Poor）用チャンネル (06)。</summary>
        public const string Poor = "06";
        /// <summary>BGAレイヤー映像用チャンネル (07)。</summary>
        public const string Layer = "07";
        /// <summary>拡張BPM変更イベント用チャンネル (08)。</summary>
        public const string Bpm = "08";
        /// <summary>ストップシーケンスイベント用チャンネル (09)。</summary>
        public const string Stop = "09";
    }

    private readonly BmsonFormat _bmson = bmson;
    private readonly PulseToBmsTimeCalculator _timeCalc = timeCalc;
    private readonly PulseToRealTimeCalculator _realTimeCalc = realTimeCalc;
    private readonly AudioSliceManager _audioSliceManager = audioSliceManager;
    private readonly bool _keyNotesOnly = keyNotesOnly;
    private int _radix = RadixBase62; // Default, will be recalculated
    private readonly bool _isDoublePlay = DetermineIsDoublePlay(bmson);
    private static readonly Logger<BmsScoreGenerator> s_logger = new();

    private static readonly string[] MeasureStrings = GenerateMeasureStrings();

    private static string[] GenerateMeasureStrings()
    {
        var arr = new string[1000];
        for (int i = 0; i < 1000; i++)
        {
            arr[i] = i.ToString("D3");
        }
        return arr;
    }

    // #WAV定義の管理
    private readonly ConcurrentDictionary<(string, int), string> _wavDefinitions = new();
    private int _wavCounter = 1;

    // #BMP定義の管理
    private readonly Dictionary<int, string> _bmpDefinitions = [];
    private int _bmpCounter = 1;

    // #BPM定義の管理
    private readonly Dictionary<double, string> _bpmDefinitions = [];
    private int _bpmCounter = 1;

    // #STOP定義の管理
    private readonly Dictionary<long, string> _stopDefinitions = [];
    private int _stopCounter = 1;

    private class ChannelLayer
    {
        public readonly string[] Notes;
        public int CurrentGcd;

        public ChannelLayer(int length)
        {
            Notes = new string[length];
            Array.Fill(Notes, RestValue);
            CurrentGcd = length;
        }

        public void SetNote(int step, string id)
        {
            Notes[step] = id;
            CurrentGcd = GCD(CurrentGcd, step);
        }

        private static int GCD(int a, int b)
        {
            while (b != 0)
            {
                int temp = b;
                b = a % b;
                a = temp;
            }
            return a;
        }
    }

    // 小節ごとのデータ管理: measure -> channel -> list of ChannelLayer
    private readonly Dictionary<int, Dictionary<string, List<ChannelLayer>>> _measures = [];

    // Y座標の事前計算データ
    private readonly struct YPositionData(double timeSec, int measure, int measureLength, int stepIndex)
    {
        public readonly double TimeSec = timeSec;
        public readonly int Measure = measure;
        public readonly int MeasureLength = measureLength;
        public readonly int StepIndex = stepIndex;
    }
    private Dictionary<long, YPositionData> _yDataMap = [];

    public string GenerateBmsText(IProgress<int>? progress = null)
    {
        Logger.ClearAccumulated();
        s_logger.WriteDebug("Start GenerateBmsText");
        var timer = s_logger.StartTimer();

        progress?.Report(5);

        // 0. Y座標データの事前計算 (次元 of 分離)
        PrecalculateYPositions();
        s_logger.WriteDebug($"PrecalculateYPositions: {timer.Lap("PrecalculateYPositions")} ms");

        // 音声ソースの投機的並列プリロード
        progress?.Report(10);
        PreloadAudioSources();
        s_logger.WriteDebug($"  [BmsScoreGenerator] PreloadAudioSources (Parallel): {timer.Lap("PreloadAudioSources")} ms");

        // 1. Choose optimal radix based on total upper bound notes
        progress?.Report(15);
        int totalNotesUpperBound = _bmson.SoundChannels?.Sum(static c => c.Notes?.Count ?? 0) ?? 0;
        _radix = totalNotesUpperBound <= MaxNumberBase36 ? RadixBase36 : RadixBase62;

        ProcessSoundChannels(progress);
        s_logger.WriteDebug($"ProcessSoundChannels: {timer.Lap("ProcessSoundChannels")} ms");
        s_logger.PrintAccumulatedGrouped("AudioSliceManager Metrics (Grouped by Channel)", LogLevel.Debug);

        ProcessBpmEvents();
        ProcessStopEvents();
        ProcessBgaEvents();
        ProcessMeasureLengths();
        progress?.Report(90);
        s_logger.WriteDebug($"Other events processing: {timer.Lap("OtherEventsProcessing")} ms");

        var sb = new StringBuilder(262144);

        // 1. ヘッダー出力
        WriteHeader(sb);

        // 2. 定義出力
        WriteDefinitions(sb);

        // 3. データブロック出力
        WriteDataBlocks(sb);

        s_logger.WriteDebug($"StringBuilder formatting: {timer.Lap("StringBuilderFormatting")} ms");
        progress?.Report(100);
        return sb.ToString();
    }

    private void PrecalculateYPositions()
    {
        var ySet = new HashSet<long>();

        if (_bmson.SoundChannels != null)
        {
            foreach (var ch in _bmson.SoundChannels)
            {
                if (ch.Notes == null) continue;
                foreach (var n in ch.Notes)
                {
                    ySet.Add(n.Y);
                    if (n.L > 0 && n.X > 0)
                    {
                        ySet.Add(n.Y + n.L);
                    }
                }
            }
        }

        if (_bmson.BpmEvents != null)
        {
            foreach (var b in _bmson.BpmEvents) ySet.Add(b.Y);
        }

        if (_bmson.StopEvents != null)
        {
            foreach (var s in _bmson.StopEvents) ySet.Add(s.Y);
        }

        if (_bmson.Bga != null)
        {
            if (_bmson.Bga.BgaEvents != null) foreach (var e in _bmson.Bga.BgaEvents) ySet.Add(e.Y);
            if (_bmson.Bga.LayerEvents != null) foreach (var e in _bmson.Bga.LayerEvents) ySet.Add(e.Y);
            if (_bmson.Bga.PoorEvents != null) foreach (var e in _bmson.Bga.PoorEvents) ySet.Add(e.Y);
        }

        _yDataMap = ySet.ToDictionary(y => y, y =>
        {
            int m = _timeCalc.GetMeasureNumber(y);
            int mLen = (int)_timeCalc.GetMeasureLength(m);
            return new YPositionData(
                _realTimeCalc.GetTimeSec(y),
                m,
                mLen,
                _timeCalc.GetStepIndex(y, mLen)
            );
        });
    }

    private void WriteHeader(StringBuilder sb)
    {
        sb.AppendLine(BmsHeader.Format(BmsHeader.Player, _isDoublePlay ? "3" : "1"));

        // GENRE
        if (!string.IsNullOrWhiteSpace(_bmson.Info.Genre))
            sb.AppendLine(BmsHeader.Format(BmsHeader.Genre, _bmson.Info.Genre));

        // TITLE
        if (!string.IsNullOrWhiteSpace(_bmson.Info.Title))
            sb.AppendLine(BmsHeader.Format(BmsHeader.Title, _bmson.Info.Title));

        // ARTIST
        if (!string.IsNullOrWhiteSpace(_bmson.Info.Artist))
            sb.AppendLine(BmsHeader.Format(BmsHeader.Artist, _bmson.Info.Artist));

        // SUBARTIST
        if (_bmson.Info.Subartists?.Count > 0)
        {
            sb.AppendLine(BmsHeader.Format(BmsHeader.Subartist, string.Join(" ", _bmson.Info.Subartists)));
        }

        // BPM
        sb.AppendLine(BmsHeader.Format(BmsHeader.BpmPrefix, Math.Round(_bmson.Info.InitBpm, 3)));

        // PLAYLEVEL
        sb.AppendLine(BmsHeader.Format(BmsHeader.PlayLevel, _bmson.Info.Level));

        // RANK
        int rank = 3; // Easy
        if (_bmson.Info.JudgeRank <= 33) rank = 0; // Very Hard
        else if (_bmson.Info.JudgeRank <= 66) rank = 1; // Hard
        else if (_bmson.Info.JudgeRank <= 99) rank = 2; // Normal
        sb.AppendLine(BmsHeader.Format(BmsHeader.Rank, rank));

        // TOTAL (Approach B: bmsonの%トータル値が100%の場合は省略し、それ以外はプレイアブルノーツ数から算出した絶対値を実数で出力)
        int playableNotes = _bmson.SoundChannels?
            .Sum(static ch => ch.Notes?.Count(static n => n.X > 0) ?? 0) ?? 0;

        if (Math.Abs(_bmson.Info.Total - DefaultPercentage) > 0.0001 && playableNotes > 0)
        {
            // bmsonの基準式（black train近似式）により、100%時のデフォルト値を計算
            double defaultTotal = Math.Max(
                MinimumFloor,
                IidxMultiplier * playableNotes /
                ((IidxNotesCoefficient * playableNotes) + IidxConstantTerm)
            );

            // %値を掛け合わせて絶対値を算出
            double realTotal = defaultTotal * (_bmson.Info.Total / DefaultPercentage);
            sb.AppendLine(BmsHeader.Format(BmsHeader.Total, Math.Round(realTotal, 4)));
        }

        // LNTYPE (bmsonのLNはType1相当だが、BMSでの互換性のためにLNTYPE 1を指定)
        sb.AppendLine(BmsHeader.Format(BmsHeader.LnType, "1"));
    }

    private void WriteDefinitions(StringBuilder sb)
    {
        sb.AppendLine();

        // WAV
        foreach (var kvp in _wavDefinitions.OrderBy(k => k.Value))
        {
            string fileName = kvp.Key.Item1;
            sb.AppendLine(BmsHeader.FormatIndexed(BmsHeader.WavPrefix, kvp.Value, fileName));
        }

        // BMP
        if (_bmpDefinitions.Count > 0) sb.AppendLine();
        foreach (var kvp in _bmpDefinitions.OrderBy(k => k.Value))
        {
            sb.AppendLine(BmsHeader.FormatIndexed(BmsHeader.BmpPrefix, kvp.Value, _bmson.Bga?.BgaHeader?.FirstOrDefault(h => h.Id == kvp.Key)?.Name));
        }

        // BPM
        if (_bpmDefinitions.Count > 0) sb.AppendLine();
        foreach (var kvp in _bpmDefinitions.OrderBy(k => k.Value))
        {
            sb.AppendLine(BmsHeader.FormatIndexed(BmsHeader.BpmPrefix, kvp.Value, kvp.Key));
        }

        // STOP
        if (_stopDefinitions.Count > 0) sb.AppendLine();
        foreach (var kvp in _stopDefinitions.OrderBy(k => k.Value))
        {
            sb.AppendLine(BmsHeader.FormatIndexed(BmsHeader.StopPrefix, kvp.Value, kvp.Key));
        }
    }

    private void WriteDataBlocks(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("*---------------------- MAIN DATA FIELD");

        if (_measures.Count == 0) return;

        int maxMeasure = _measures.Keys.Max();

        for (int m = 0; m <= maxMeasure; m++)
        {
            if (!_measures.ContainsKey(m)) continue;
            var channels = _measures[m];

            sb.AppendLine(); // 空行で小節を区切る

            // チャンネルごとにソートして出力
            foreach (var ch in channels.Keys.Order())
            {
                foreach (var layer in channels[ch])
                {
                    string mStr = (m < 1000) ? MeasureStrings[m] : m.ToString("D3");
                    sb.Append('#').Append(mStr).Append(ch).Append(':');

                    int gcd = layer.CurrentGcd;
                    for (int i = 0; i < layer.Notes.Length; i += gcd)
                    {
                        sb.Append(layer.Notes[i]);
                    }
                    sb.AppendLine();
                }
            }
        }
    }

    private readonly struct PendingNote(int measure, string channel, int step, int measureLength, string id)
    {
        public readonly int Measure = measure;
        public readonly string Channel = channel;
        public readonly int Step = step;
        public readonly int MeasureLength = measureLength;
        public readonly string Id = id;
    }

    private readonly struct NoteBlock(int start, int count)
    {
        public readonly int Start = start;
        public readonly int Count = count;
    }

    private static List<NoteBlock> SplitNotesIntoBlocks(List<BmsonNote> notes)
    {
        var blocks = new List<NoteBlock>();
        if (notes.Count == 0) return blocks;

        int currentStart = 0;
        int currentCount = 0;

        for (int i = 0; i < notes.Count; i++)
        {
            if (!notes[i].C || currentCount == 0)
            {
                if (currentCount > 0)
                {
                    blocks.Add(new NoteBlock(currentStart, currentCount));
                }
                currentStart = i;
                currentCount = 1;
            }
            else
            {
                currentCount++;
            }
        }
        if (currentCount > 0)
        {
            blocks.Add(new NoteBlock(currentStart, currentCount));
        }

        return blocks;
    }

    private void PreloadAudioSources()
    {
        if (_bmson.SoundChannels == null) return;
        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
        Parallel.ForEach(_bmson.SoundChannels, options, ch =>
        {
            if (ch.Notes == null || ch.Notes.Count == 0) return;
            _audioSliceManager.PreloadAudioSource(ch.Name);
        });
    }

    private void ProcessSoundChannels(IProgress<int>? progress = null)
    {
        if (_bmson.SoundChannels == null) return;

        int totalNotes = _bmson.SoundChannels.Sum(ch => ch.Notes?.Count ?? 0);
        if (totalNotes == 0) return;

        var pendingNotes = new PendingNote[totalNotes * 2]; // *2 for LNs
        int[] sharedNoteIndex = [0];

        int totalChannels = _bmson.SoundChannels.Count;
        int processedChannels = 0;

        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
        Parallel.ForEach(_bmson.SoundChannels, options, ch =>
        {
            ProcessChannel(ch, pendingNotes, sharedNoteIndex);
            if (progress != null)
            {
                int p = Interlocked.Increment(ref processedChannels);
                progress.Report(15 + (int)(p * 75.0 / totalChannels));
            }
        });

        for (int i = 0; i < sharedNoteIndex[0]; i++)
        {
            var note = pendingNotes[i];
            AddNoteDirect(note.Measure, note.Channel, note.Step, note.MeasureLength, note.Id);
        }
    }

    private void ProcessChannel(BmsonSoundChannel ch, PendingNote[] pendingNotes, int[] sharedNoteIndex)
    {
        if (ch.Notes == null || ch.Notes.Count == 0) return;

        var blocks = SplitNotesIntoBlocks(ch.Notes);

        var innerOptions = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };

        Parallel.For(0, blocks.Count, innerOptions, bIndex =>
        {
            var block = blocks[bIndex];
            double blockStartSec = _yDataMap[ch.Notes[block.Start].Y].TimeSec;

            double nextBlockStartSec = bIndex + 1 < blocks.Count
                ? _yDataMap[ch.Notes[blocks[bIndex + 1].Start].Y].TimeSec
                : double.PositiveInfinity;

            ProcessBlock(ch.Name, ch.Notes, block, blockStartSec, nextBlockStartSec, pendingNotes, sharedNoteIndex);
        });
    }

    private void ProcessBlock(string channelName, List<BmsonNote> allNotes, NoteBlock block, double blockStartSec, double nextBlockStartSec, PendingNote[] pendingNotes, int[] sharedNoteIndex)
    {
        // depth は「ブロック内でのインデックス」に代数的に等価
        for (int depth = 0; depth < block.Count; depth++)
        {
            int noteIndex = block.Start + depth;
            var n = allNotes[noteIndex];

            if (_keyNotesOnly && n.X == 0) continue;

            double currentSec = _yDataMap[n.Y].TimeSec;
            double oSec = currentSec - blockStartSec;
            double nextSec = nextBlockStartSec;
            for (int k = depth + 1; k < block.Count; k++)
            {
                var nextNote = allNotes[block.Start + k];
                if (nextNote.Y > n.Y)
                {
                    nextSec = _yDataMap[nextNote.Y].TimeSec;
                    break;
                }
            }
            double dSec = nextSec - currentSec;

            string sliceFile = _audioSliceManager.SliceAudio(channelName, oSec, dSec);
            if (string.IsNullOrEmpty(sliceFile)) continue;

            string wavId = GetWavId(sliceFile, depth);
            var yData = _yDataMap[n.Y];

            if (n.L > 0 && n.X > 0)
            {
                string lnChannel = MapLaneToChannel(n.X, true);
                var endYData = _yDataMap[n.Y + n.L];

                int idx1 = Interlocked.Add(ref sharedNoteIndex[0], 2) - 2;
                pendingNotes[idx1] = new PendingNote(yData.Measure, lnChannel, yData.StepIndex, yData.MeasureLength, wavId);
                pendingNotes[idx1 + 1] = new PendingNote(endYData.Measure, lnChannel, endYData.StepIndex, endYData.MeasureLength, wavId);
            }
            else
            {
                string bmsChannel = MapLaneToChannel(n.X, false);
                string targetChannel = bmsChannel;
                int idx = Interlocked.Increment(ref sharedNoteIndex[0]) - 1;
                pendingNotes[idx] = new PendingNote(yData.Measure, targetChannel, yData.StepIndex, yData.MeasureLength, wavId);
            }
        }
    }

    private void ProcessBpmEvents()
    {
        if (_bmson.BpmEvents == null) return;
        foreach (var b in _bmson.BpmEvents)
        {
            double roundedBpm = Math.Round(b.Bpm, 3);
            if (!_bpmDefinitions.TryGetValue(roundedBpm, out string? bpmId))
            {
                bpmId = RadixConvert.IntToZZ(_bpmCounter++, RadixBase36);
                _bpmDefinitions[roundedBpm] = bpmId;
            }

            var yData = _yDataMap[b.Y];
            AddNote(yData.Measure, BmsChannel.Bpm, yData.StepIndex, yData.MeasureLength, bpmId);
        }
    }

    private void ProcessStopEvents()
    {
        if (_bmson.StopEvents == null) return;
        foreach (var s in _bmson.StopEvents)
        {
            long bmsStopVal = s.Duration * 48 / _bmson.Info.Resolution;

            if (!_stopDefinitions.TryGetValue(bmsStopVal, out string? stopId))
            {
                stopId = RadixConvert.IntToZZ(_stopCounter++, RadixBase36);
                _stopDefinitions[bmsStopVal] = stopId;
            }

            var yData = _yDataMap[s.Y];
            AddNote(yData.Measure, "09", yData.StepIndex, yData.MeasureLength, stopId);
        }
    }

    private void ProcessBgaEvents()
    {
        if (_bmson.Bga == null) return;

        // Header mapping
        foreach (var h in _bmson.Bga.BgaHeader)
        {
            if (!_bmpDefinitions.ContainsKey(h.Id))
            {
                _bmpDefinitions[h.Id] = RadixConvert.IntToZZ(_bmpCounter++, RadixBase36);
            }
        }

        void AddBgaEvents(List<BmsonBgaEvent> events, string channel)
        {
            if (events == null) return;
            foreach (var e in events)
            {
                if (_bmpDefinitions.TryGetValue(e.Id, out string? bmpId))
                {
                    var yData = _yDataMap[e.Y];
                    AddNote(yData.Measure, channel, yData.StepIndex, yData.MeasureLength, bmpId);
                }
            }
        }

        AddBgaEvents(_bmson.Bga.BgaEvents, "04");
        AddBgaEvents(_bmson.Bga.LayerEvents, "07");
        AddBgaEvents(_bmson.Bga.PoorEvents, "06");
    }

    private void ProcessMeasureLengths()
    {
        if (_bmson.Lines == null) return;

        int maxM = _measures.Count > 0 ? _measures.Keys.Max() : 0;
        int mCount = Math.Max(maxM, _timeCalc.GetMeasureNumber(_bmson.Lines.LastOrDefault()?.Y ?? 0));

        for (int m = 0; m <= mCount; m++)
        {
            double mult = _timeCalc.GetMeterMultiplier(m);
            // 4/4からずれている場合のみ出力
            if (Math.Abs(mult - 1.0) > 0.0001)
            {
                string multStr = mult.ToString("0.000000").TrimEnd('0').TrimEnd('.');
                if (!_measures.ContainsKey(m)) _measures[m] = [];
                if (!_measures[m].ContainsKey(BmsChannel.Meter))
                {
                    var layer = new ChannelLayer(1);
                    layer.SetNote(0, multStr);
                    _measures[m][BmsChannel.Meter] = [layer];
                }
            }
        }
    }

    private string GetWavId(string fileName, int depth = 0)
    {
        var key = (fileName, depth);
        return _wavDefinitions.GetOrAdd(key, _ =>
        {
            int counter = Interlocked.Increment(ref _wavCounter) - 1;
            return RadixConvert.IntToZZ(counter, _radix);
        });
    }

    private void AddNote(int measure, string channel, int step, int measureLength, string id)
    {
        lock (_measures)
        {
            AddNoteDirect(measure, channel, step, measureLength, id);
        }
    }

    private void AddNoteDirect(int measure, string channel, int step, int measureLength, string id)
    {
        if (!_measures.TryGetValue(measure, out var mDict))
        {
            mDict = [];
            _measures[measure] = mDict;
        }

        if (!mDict.TryGetValue(channel, out var layers))
        {
            layers = [new ChannelLayer(measureLength)];
            mDict[channel] = layers;
        }

        if (channel == BmsChannel.Bgm)
        {
            bool placed = false;
            foreach (var layer in layers)
            {
                if (layer.Notes.Length == measureLength && layer.Notes[step] == RestValue)
                {
                    layer.SetNote(step, id);
                    placed = true;
                    break;
                }
            }
            if (!placed)
            {
                var newLayer = new ChannelLayer(measureLength);
                newLayer.SetNote(step, id);
                layers.Add(newLayer);
            }
        }
        else
        {
            if (layers[0].Notes.Length == measureLength)
            {
                if (layers[0].Notes[step] != RestValue)
                {
                    // 同一レーン・同一タイミングでの衝突(和音)はBGMレーンに退避させる
                    AddNoteDirect(measure, BmsChannel.Bgm, step, measureLength, id);
                }
                else
                {
                    layers[0].SetNote(step, id);
                }
            }
        }
    }

    private static string MapLaneToChannel(int x, bool isLn)
    {
        if (x == 0) return BmsChannel.Bgm;

        int prefix = (x <= 8) ? (isLn ? 5 : 1) : (isLn ? 6 : 2);
        int suffix = (((x - 1) % 8) + 1) switch
        {
            6 => 8,
            7 => 9,
            8 => 6,
            int n => n
        };

        return $"{prefix}{suffix}";
    }

    private static bool DetermineIsDoublePlay(BmsonFormat bmson)
    {
        if (bmson.SoundChannels == null) return false;
        foreach (var ch in bmson.SoundChannels)
        {
            if (ch.Notes == null) continue;
            foreach (var n in ch.Notes)
            {
                if (n.X >= 9) return true;
            }
        }
        return false;
    }
}


