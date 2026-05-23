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

    // #WAV定義の管理
    private readonly ConcurrentDictionary<string, string> _wavDefinitions = new();
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

    // 小節ごとのデータ管理: measure -> channel -> list of 240-step string arrays
    private readonly Dictionary<int, Dictionary<string, List<string[]>>> _measures = [];

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

        // 1. Pre-pass slicing to determine exact number of unique definitions needed
        PreSliceAudio();
        int uniqueSlices = _audioSliceManager.GetGeneratedSliceCount();
        PerformanceDebugLogger.WriteLine($"  [BmsScoreGenerator] PreSliceAudio (uniqueSlices={uniqueSlices}): {timer.Lap("PreSliceAudio")} ms");
        PerformanceDebugLogger.PrintAccumulated("    [AudioSliceManager Metrics]");

        // 2. Choose optimal radix
        _radix = uniqueSlices <= AppConstants.Definition.MaxNumberBase36 ? AppConstants.Definition.RadixBase36 : AppConstants.Definition.RadixBase62;

        ProcessSoundChannels();
        PerformanceDebugLogger.WriteLine($"  [BmsScoreGenerator] ProcessSoundChannels: {timer.Lap("ProcessSoundChannels")} ms");

        ProcessBpmEvents();
        ProcessStopEvents();
        ProcessBgaEvents();
        ProcessMeasureLengths();
        PerformanceDebugLogger.WriteLine($"  [BmsScoreGenerator] Other events processing: {timer.Lap("OtherEventsProcessing")} ms");

        var sb = new StringBuilder();

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
        if (_bmson.Info.Subartists != null && _bmson.Info.Subartists.Count > 0)
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

        // TOTAL
        sb.AppendLine($"#TOTAL {_bmson.Info.Total}");

        // LNTYPE (bmsonのLNはType1相当だが、BMSでの互換性のためにLNTYPE 1を指定)
        sb.AppendLine("#LNTYPE 1");
    }

    private void WriteDefinitions(StringBuilder sb)
    {
        sb.AppendLine();

        // WAV
        foreach (var kvp in _wavDefinitions.OrderBy(k => k.Value))
        {
            string fileName = kvp.Key.Split('|')[0];
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
            foreach (var ch in channels.Keys.OrderBy(k => k))
            {
                foreach (var arr in channels[ch])
                {
                    string mStr = m.ToString("D3");
                    string dataStr = string.Join("", DownSampleArray(arr));

                    // すべて00の場合は出力しない（ただし小節長変更02などは出力する）
                    if (ch != "02" && dataStr.All(c => c == '0'))
                    {
                        continue;
                    }

                    sb.AppendLine($"#{mStr}{ch}:{dataStr}");
                }
            }
        }
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

    private static string[] DownSampleArray(string[] arr)
    {
        int gcd = arr.Length;
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] != AppConstants.Definition.Rest)
            {
                gcd = GCD(gcd, i);
            }
        }

        if (gcd > 1)
        {
            int newLength = arr.Length / gcd;
            var newArr = new string[newLength];
            for (int i = 0; i < newLength; i++)
            {
                newArr[i] = arr[i * gcd];
            }
            return newArr;
        }
        return arr;
    }

    private void PreSliceAudio()
    {
        if (_bmson.SoundChannels == null) return;

        Parallel.ForEach(_bmson.SoundChannels, ch =>
        {
            if (ch.Notes == null || ch.Notes.Count == 0) return;

            // 連(Block)ごとに分割して依存関係を排除
            var blocks = new List<List<BmsonNote>>();
            List<BmsonNote>? currentBlock = null;

            foreach (var n in ch.Notes)
            {
                if (!n.C || currentBlock == null)
                {
                    currentBlock = [];
                    blocks.Add(currentBlock);
                }
                currentBlock.Add(n);
            }

            for (int bIndex = 0; bIndex < blocks.Count; bIndex++)
            {
                var block = blocks[bIndex];
                double blockStartSec = _yDataMap[block[0].Y].TimeSec;

                double nextBlockStartSec = double.PositiveInfinity;
                if (bIndex + 1 < blocks.Count)
                {
                    nextBlockStartSec = _yDataMap[blocks[bIndex + 1][0].Y].TimeSec;
                }

                foreach (var n in block)
                {
                    if (_keyNotesOnly && n.X == 0) continue;

                    double currentSec = _yDataMap[n.Y].TimeSec;
                    double oSec = currentSec - blockStartSec;
                    double dSec = nextBlockStartSec - currentSec;

                    _audioSliceManager.SliceAudio(ch.Name, oSec, dSec);
                }
            }
        });
    }

    private void ProcessSoundChannels()
    {
        if (_bmson.SoundChannels == null) return;

        Parallel.ForEach(_bmson.SoundChannels, ch =>
        {
            if (ch.Notes == null || ch.Notes.Count == 0) return;

            // 連(Block)ごとに分割して依存関係を排除
            var blocks = new List<List<BmsonNote>>();
            List<BmsonNote>? currentBlock = null;

            foreach (var n in ch.Notes)
            {
                if (!n.C || currentBlock == null)
                {
                    currentBlock = [];
                    blocks.Add(currentBlock);
                }
                currentBlock.Add(n);
            }

            for (int bIndex = 0; bIndex < blocks.Count; bIndex++)
            {
                var block = blocks[bIndex];
                double blockStartSec = _yDataMap[block[0].Y].TimeSec;

                double nextBlockStartSec = double.PositiveInfinity;
                if (bIndex + 1 < blocks.Count)
                {
                    nextBlockStartSec = _yDataMap[blocks[bIndex + 1][0].Y].TimeSec;
                }

                // depth は「ブロック内でのインデックス」に代数的に等価
                for (int depth = 0; depth < block.Count; depth++)
                {
                    var n = block[depth];

                    if (_keyNotesOnly && n.X == 0) continue;

                    double currentSec = _yDataMap[n.Y].TimeSec;
                    double oSec = currentSec - blockStartSec;
                    double dSec = nextBlockStartSec - currentSec;

                    string sliceFile = _audioSliceManager.SliceAudio(ch.Name, oSec, dSec);
                    if (string.IsNullOrEmpty(sliceFile)) continue;

                    string wavId = GetWavId(sliceFile, depth);
                    string bmsChannel = MapLaneToChannel(n.X, false);
                    var yData = _yDataMap[n.Y];

                    if (n.L > 0 && n.X > 0)
                    {
                        string lnChannel = MapLaneToChannel(n.X, true);
                        var endYData = _yDataMap[n.Y + n.L];

                        // LNの開始と終了をLNチャンネルに配置
                        AddNote(yData.Measure, lnChannel, yData.StepIndex, yData.MeasureLength, wavId);
                        AddNote(endYData.Measure, lnChannel, endYData.StepIndex, endYData.MeasureLength, wavId);
                    }
                    else
                    {
                        // 鍵盤レーンであっても、depth > 0 (和音)の場合はBGMレーンに逃がす
                        if (n.X > 0 && depth > 0)
                        {
                            AddNote(yData.Measure, "01", yData.StepIndex, yData.MeasureLength, wavId);
                        }
                        else
                        {
                            AddNote(yData.Measure, bmsChannel, yData.StepIndex, yData.MeasureLength, wavId);
                        }
                    }
                }
            }
        });
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
            long bmsStopVal = (s.Duration * 48) / _bmson.Info.Resolution;

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
                if (!_measures[m].ContainsKey("02")) _measures[m]["02"] = [[multStr]];
            }
        }
    }

    private string GetWavId(string fileName, int depth = 0)
    {
        string key = $"{fileName}|{depth}";
        return _wavDefinitions.GetOrAdd(key, _ =>
        {
            int counter = Interlocked.Increment(ref _wavCounter) - 1;
            return RadixConvert.IntToZZ(counter, _radix);
        });
    }

    private static string[] CreateEmptyArray(int size)
    {
        var arr = new string[size];
        for (int i = 0; i < size; i++) arr[i] = AppConstants.Definition.Rest;
        return arr;
    }

    private void AddNote(int measure, string channel, int step, int measureLength, string id)
    {
        lock (_measures)
        {
            if (!_measures.ContainsKey(measure)) _measures[measure] = [];
            var mDict = _measures[measure];

            if (!mDict.ContainsKey(channel)) mDict[channel] = [CreateEmptyArray(measureLength)];

            if (channel == "01")
            {
                bool placed = false;
                foreach (var arr in mDict[channel])
                {
                    if (arr.Length == measureLength && arr[step] == AppConstants.Definition.Rest)
                    {
                        arr[step] = id;
                        placed = true;
                        break;
                    }
                }
                if (!placed)
                {
                    var newArr = CreateEmptyArray(measureLength);
                    newArr[step] = id;
                    mDict[channel].Add(newArr);
                }
            }
            else
            {
                if (mDict[channel][0].Length == measureLength)
                {
                    mDict[channel][0][step] = id;
                }
            }
        }
    }

    private static string MapLaneToChannel(int x, bool isLn)
    {
        if (x == 0) return "01";

        int prefix = (x <= 8) ? (isLn ? 5 : 1) : (isLn ? 6 : 2);
        int suffix = ((x - 1) % 8 + 1) switch
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
