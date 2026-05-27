using System.Collections.Concurrent;
using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Bms.Bmson;

/// <summary>
/// BmsonのデータモデルとスライスされたWAVから、BMSファイルのテキストを生成するジェネレータ。
/// </summary>
public class BmsScoreGenerator(
    BmsonFormat bmson,
    PulseToBmsTimeCalculator timeCalc,
    PulseToRealTimeCalculator realTimeCalc,
    AudioSliceManager audioSliceManager,
    bool keyNotesOnly = false)
{
    private readonly BmsonFormat _bmson = bmson;
    private readonly PulseToBmsTimeCalculator _timeCalc = timeCalc;
    private readonly PulseToRealTimeCalculator _realTimeCalc = realTimeCalc;
    private readonly AudioSliceManager _audioSliceManager = audioSliceManager;
    private readonly bool _keyNotesOnly = keyNotesOnly;
    private int _radix = AppConstants.Definition.RadixBase62; // Default, will be recalculated
    private readonly bool _isDoublePlay = DetermineIsDoublePlay(bmson);

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
            Array.Fill(Notes, AppConstants.Definition.Rest);
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
    private class YPositionData
    {
        public double TimeSec { get; set; }
        public int Measure { get; set; }
        public int MeasureLength { get; set; }
        public int StepIndex { get; set; }
    }
    private Dictionary<long, YPositionData> _yDataMap = [];

    public string GenerateBmsText()
    {
        PerformanceDebugLogger.ClearAccumulated();
        PerformanceDebugLogger.WriteLine("  [BmsScoreGenerator] Start GenerateBmsText");
        var timer = PerformanceDebugLogger.StartTimer();

        // 0. Y座標データの事前計算 (次元 of 分離)
        PrecalculateYPositions();
        PerformanceDebugLogger.WriteLine($"  [BmsScoreGenerator] PrecalculateYPositions: {timer.Lap("PrecalculateYPositions")} ms");

        // 音声ソースの投機的並列プリロード
        PreloadAudioSources();
        PerformanceDebugLogger.WriteLine($"  [BmsScoreGenerator] PreloadAudioSources (Parallel): {timer.Lap("PreloadAudioSources")} ms");

        // 1. Choose optimal radix based on total upper bound notes
        int totalNotesUpperBound = _bmson.SoundChannels?.Sum(static c => c.Notes?.Count ?? 0) ?? 0;
        _radix = totalNotesUpperBound <= AppConstants.Definition.MaxNumberBase36 ? AppConstants.Definition.RadixBase36 : AppConstants.Definition.RadixBase62;

        ProcessSoundChannels();
        PerformanceDebugLogger.WriteLine($"  [BmsScoreGenerator] ProcessSoundChannels: {timer.Lap("ProcessSoundChannels")} ms");
        PerformanceDebugLogger.PrintAccumulatedGrouped("AudioSliceManager Metrics (Grouped by Channel)", LogLevel.Debug);

        ProcessBpmEvents();
        ProcessStopEvents();
        ProcessBgaEvents();
        ProcessMeasureLengths();
        PerformanceDebugLogger.WriteLine($"  [BmsScoreGenerator] Other events processing: {timer.Lap("OtherEventsProcessing")} ms");

        var sb = new StringBuilder(262144);

        // 1. ヘッダー出力
        WriteHeader(sb);

        // 2. 定義出力
        WriteDefinitions(sb);

        // 3. データブロック出力
        WriteDataBlocks(sb);

        PerformanceDebugLogger.WriteLine($"  [BmsScoreGenerator] StringBuilder formatting: {timer.Lap("StringBuilderFormatting")} ms");
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
            return new YPositionData
            {
                TimeSec = _realTimeCalc.GetTimeSec(y),
                Measure = m,
                MeasureLength = mLen,
                StepIndex = _timeCalc.GetStepIndex(y, mLen)
            };
        });
    }

    private void WriteHeader(StringBuilder sb)
    {
        sb.AppendLine(_isDoublePlay ? "#PLAYER 3" : "#PLAYER 1");

        // GENRE
        if (!string.IsNullOrWhiteSpace(_bmson.Info.Genre))
            sb.AppendLine($"#GENRE {_bmson.Info.Genre}");

        // TITLE
        if (!string.IsNullOrWhiteSpace(_bmson.Info.Title))
            sb.AppendLine($"#TITLE {_bmson.Info.Title}");

        // ARTIST
        if (!string.IsNullOrWhiteSpace(_bmson.Info.Artist))
            sb.AppendLine($"#ARTIST {_bmson.Info.Artist}");

        // SUBARTIST
        if (_bmson.Info.Subartists?.Count > 0)
        {
            sb.AppendLine($"#SUBARTIST {string.Join(" ", _bmson.Info.Subartists)}");
        }

        // BPM
        sb.AppendLine($"{AppConstants.Definition.BpmPrefix} {Math.Round(_bmson.Info.InitBpm, 3)}");

        // PLAYLEVEL
        sb.AppendLine($"#PLAYLEVEL {_bmson.Info.Level}");

        // RANK
        int rank = 3; // Easy
        if (_bmson.Info.JudgeRank <= 33) rank = 0; // Very Hard
        else if (_bmson.Info.JudgeRank <= 66) rank = 1; // Hard
        else if (_bmson.Info.JudgeRank <= 99) rank = 2; // Normal
        sb.AppendLine($"#RANK {rank}");

        // TOTAL (Approach B: bmsonの%トータル値が100%の場合は省略し、それ以外はプレイアブルノーツ数から算出した絶対値を実数で出力)
        int playableNotes = _bmson.SoundChannels?
            .Sum(static ch => ch.Notes?.Count(static n => n.X > 0) ?? 0) ?? 0;

        if (Math.Abs(_bmson.Info.Total - AppConstants.BmsTotal.DefaultPercentage) > 0.0001 && playableNotes > 0)
        {
            // bmsonの基準式（black train近似式）により、100%時のデフォルト値を計算
            double defaultTotal = Math.Max(
                AppConstants.BmsTotal.MinimumFloor,
                AppConstants.BmsTotal.IidxMultiplier * playableNotes /
                ((AppConstants.BmsTotal.IidxNotesCoefficient * playableNotes) + AppConstants.BmsTotal.IidxConstantTerm)
            );

            // %値を掛け合わせて絶対値を算出
            double realTotal = defaultTotal * (_bmson.Info.Total / AppConstants.BmsTotal.DefaultPercentage);
            sb.AppendLine($"#TOTAL {Math.Round(realTotal, 4)}");
        }

        // LNTYPE (bmsonのLNはType1相当だが、BMSでの互換性のためにLNTYPE 1を指定)
        sb.AppendLine("#LNTYPE 1");
    }

    private void WriteDefinitions(StringBuilder sb)
    {
        sb.AppendLine();

        // WAV
        foreach (var kvp in _wavDefinitions.OrderBy(k => k.Value))
        {
            string fileName = kvp.Key.Item1;
            sb.AppendLine($"{AppConstants.Definition.WavPrefix}{kvp.Value} {fileName}");
        }

        // BMP
        if (_bmpDefinitions.Count > 0) sb.AppendLine();
        foreach (var kvp in _bmpDefinitions.OrderBy(k => k.Value))
        {
            sb.AppendLine($"{AppConstants.Definition.BmpPrefix}{kvp.Value} {_bmson.Bga?.BgaHeader?.FirstOrDefault(h => h.Id == kvp.Key)?.Name}");
        }

        // BPM
        if (_bpmDefinitions.Count > 0) sb.AppendLine();
        foreach (var kvp in _bpmDefinitions.OrderBy(k => k.Value))
        {
            sb.AppendLine($"{AppConstants.Definition.BpmPrefix}{kvp.Value} {kvp.Key}");
        }

        // STOP
        if (_stopDefinitions.Count > 0) sb.AppendLine();
        foreach (var kvp in _stopDefinitions.OrderBy(k => k.Value))
        {
            sb.AppendLine($"{AppConstants.Definition.StopPrefix}{kvp.Value} {kvp.Key}");
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

    private static List<List<BmsonNote>> SplitNotesIntoBlocks(IReadOnlyList<BmsonNote> notes)
    {
        var blocks = new List<List<BmsonNote>>();
        List<BmsonNote>? currentBlock = null;

        foreach (var n in notes)
        {
            if (!n.C || currentBlock == null)
            {
                currentBlock = [];
                blocks.Add(currentBlock);
            }
            currentBlock.Add(n);
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

    private void ProcessSoundChannels()
    {
        if (_bmson.SoundChannels == null) return;

        int totalNotes = _bmson.SoundChannels.Sum(ch => ch.Notes?.Count ?? 0);
        if (totalNotes == 0) return;

        var pendingNotes = new PendingNote[totalNotes * 2]; // *2 for LNs
        int noteIndex = 0;

        var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) };
        Parallel.ForEach(_bmson.SoundChannels, options, ch => ProcessChannel(ch, pendingNotes, ref noteIndex));

        for (int i = 0; i < noteIndex; i++)
        {
            var note = pendingNotes[i];
            AddNoteDirect(note.Measure, note.Channel, note.Step, note.MeasureLength, note.Id);
        }
    }

    private void ProcessChannel(BmsonSoundChannel ch, PendingNote[] pendingNotes, ref int noteIndex)
    {
        if (ch.Notes == null || ch.Notes.Count == 0) return;

        var blocks = SplitNotesIntoBlocks(ch.Notes);

        for (int bIndex = 0; bIndex < blocks.Count; bIndex++)
        {
            var block = blocks[bIndex];
            double blockStartSec = _yDataMap[block[0].Y].TimeSec;

            double nextBlockStartSec = bIndex + 1 < blocks.Count
                ? _yDataMap[blocks[bIndex + 1][0].Y].TimeSec
                : double.PositiveInfinity;

            ProcessBlock(ch.Name, block, blockStartSec, nextBlockStartSec, pendingNotes, ref noteIndex);
        }
    }

    private void ProcessBlock(string channelName, List<BmsonNote> block, double blockStartSec, double nextBlockStartSec, PendingNote[] pendingNotes, ref int noteIndex)
    {
        // depth は「ブロック内でのインデックス」に代数的に等価
        for (int depth = 0; depth < block.Count; depth++)
        {
            var n = block[depth];

            if (_keyNotesOnly && n.X == 0) continue;

            double currentSec = _yDataMap[n.Y].TimeSec;
            double oSec = currentSec - blockStartSec;
            double nextSec = nextBlockStartSec;
            for (int k = depth + 1; k < block.Count; k++)
            {
                if (block[k].Y > n.Y)
                {
                    nextSec = _yDataMap[block[k].Y].TimeSec;
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

                int idx1 = Interlocked.Add(ref noteIndex, 2) - 2;
                pendingNotes[idx1] = new PendingNote(yData.Measure, lnChannel, yData.StepIndex, yData.MeasureLength, wavId);
                pendingNotes[idx1 + 1] = new PendingNote(endYData.Measure, lnChannel, endYData.StepIndex, endYData.MeasureLength, wavId);
            }
            else
            {
                string bmsChannel = MapLaneToChannel(n.X, false);
                string targetChannel = bmsChannel;
                int idx = Interlocked.Increment(ref noteIndex) - 1;
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
                bpmId = RadixConvert.IntToZZ(_bpmCounter++, AppConstants.Definition.RadixBase36);
                _bpmDefinitions[roundedBpm] = bpmId;
            }

            var yData = _yDataMap[b.Y];
            AddNote(yData.Measure, "08", yData.StepIndex, yData.MeasureLength, bpmId);
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
                stopId = RadixConvert.IntToZZ(_stopCounter++, AppConstants.Definition.RadixBase36);
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
                _bmpDefinitions[h.Id] = RadixConvert.IntToZZ(_bmpCounter++, AppConstants.Definition.RadixBase36);
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
                if (!_measures[m].ContainsKey("02"))
                {
                    var layer = new ChannelLayer(1);
                    layer.SetNote(0, multStr);
                    _measures[m]["02"] = [layer];
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

        if (channel == "01")
        {
            bool placed = false;
            foreach (var layer in layers)
            {
                if (layer.Notes.Length == measureLength && layer.Notes[step] == AppConstants.Definition.Rest)
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
                if (layers[0].Notes[step] != AppConstants.Definition.Rest)
                {
                    // 同一レーン・同一タイミングでの衝突(和音)はBGMレーンに退避させる
                    AddNoteDirect(measure, "01", step, measureLength, id);
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
        if (x == 0) return "01";

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
