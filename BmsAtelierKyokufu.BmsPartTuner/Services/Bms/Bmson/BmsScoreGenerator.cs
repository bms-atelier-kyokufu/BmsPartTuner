using BmsAtelierKyokufu.BmsPartTuner.Core.Bms;
using BmsAtelierKyokufu.BmsPartTuner.Core.Helpers;
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
    private int _radix = 62; // Default, will be recalculated

    // #WAV定義の管理
    private readonly Dictionary<string, string> _wavDefinitions = [];
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

    public string GenerateBmsText()
    {
        // 1. Pre-pass slicing to determine exact number of unique definitions needed
        PreSliceAudio();
        int uniqueSlices = _audioSliceManager.GetGeneratedSliceCount();

        // 2. Choose optimal radix
        _radix = uniqueSlices <= 1295 ? 36 : 62;

        ProcessSoundChannels();
        ProcessBpmEvents();
        ProcessStopEvents();
        ProcessBgaEvents();
        ProcessMeasureLengths();

        var sb = new StringBuilder();

        // 1. ヘッダー出力
        WriteHeader(sb);

        // 2. 定義出力
        WriteDefinitions(sb);

        // 3. データブロック出力
        WriteDataBlocks(sb);

        return sb.ToString();
    }

    private void WriteHeader(StringBuilder sb)
    {
        sb.AppendLine("#PLAYER 1");

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
        sb.AppendLine($"#BPM {_bmson.Info.InitBpm}");

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
            sb.AppendLine($"#WAV{kvp.Value} {kvp.Key}");
        }

        // BMP
        if (_bmpDefinitions.Count > 0) sb.AppendLine();
        foreach (var kvp in _bmpDefinitions.OrderBy(k => k.Value))
        {
            sb.AppendLine($"#BMP{kvp.Value} {_bmson.Bga?.BgaHeader?.FirstOrDefault(h => h.Id == kvp.Key)?.Name}");
        }

        // BPM
        if (_bpmDefinitions.Count > 0) sb.AppendLine();
        foreach (var kvp in _bpmDefinitions.OrderBy(k => k.Value))
        {
            sb.AppendLine($"#BPM{kvp.Value} {kvp.Key}");
        }

        // STOP
        if (_stopDefinitions.Count > 0) sb.AppendLine();
        foreach (var kvp in _stopDefinitions.OrderBy(k => k.Value))
        {
            sb.AppendLine($"#STOP{kvp.Value} {kvp.Key}");
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
                    string dataStr = string.Join("", arr);

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

    private void PreSliceAudio()
    {
        if (_bmson.SoundChannels == null) return;

        Parallel.ForEach(_bmson.SoundChannels, ch =>
        {
            if (ch.Notes == null || ch.Notes.Count == 0) return;

            double lastCFalseSec = 0.0;
            double currentSec = _realTimeCalc.GetTimeSec(ch.Notes[0].Y);

            for (int i = 0; i < ch.Notes.Count; i++)
            {
                var n = ch.Notes[i];

                if (!n.C)
                {
                    lastCFalseSec = currentSec;
                }

                double nextSec = 0.0;
                if (i + 1 < ch.Notes.Count)
                {
                    nextSec = _realTimeCalc.GetTimeSec(ch.Notes[i + 1].Y);
                }

                if (_keyNotesOnly && n.X == 0)
                {
                    currentSec = nextSec;
                    continue;
                }

                double oSec = 0.0;
                if (n.C)
                {
                    oSec = currentSec - lastCFalseSec;
                }

                double dSec = double.PositiveInfinity;
                if (i + 1 < ch.Notes.Count)
                {
                    dSec = nextSec - currentSec;
                }

                _audioSliceManager.SliceAudio(ch.Name, oSec, dSec);

                currentSec = nextSec;
            }
        });
    }

    private void ProcessSoundChannels()
    {
        if (_bmson.SoundChannels == null) return;

        Parallel.ForEach(_bmson.SoundChannels, ch =>
        {
            if (ch.Notes == null || ch.Notes.Count == 0) return;

            double lastCFalseSec = 0.0;
            double currentSec = _realTimeCalc.GetTimeSec(ch.Notes[0].Y);

            for (int i = 0; i < ch.Notes.Count; i++)
            {
                var n = ch.Notes[i];

                if (!n.C)
                {
                    lastCFalseSec = currentSec;
                }

                double nextSec = 0.0;
                if (i + 1 < ch.Notes.Count)
                {
                    nextSec = _realTimeCalc.GetTimeSec(ch.Notes[i + 1].Y);
                }

                if (_keyNotesOnly && n.X == 0)
                {
                    currentSec = nextSec;
                    continue; // 演奏ノーツのみ抽出の場合はBGMレーンを無視
                }

                double oSec = 0.0;
                if (n.C)
                {
                    oSec = currentSec - lastCFalseSec;
                }

                double dSec = double.PositiveInfinity;
                if (i + 1 < ch.Notes.Count)
                {
                    dSec = nextSec - currentSec;
                }

                string sliceFile = _audioSliceManager.SliceAudio(ch.Name, oSec, dSec);

                currentSec = nextSec;

                if (string.IsNullOrEmpty(sliceFile)) continue;

                string wavId = GetWavId(sliceFile);
                string bmsChannel = MapLaneToChannel(n.X, false);
                int measure = _timeCalc.GetMeasureNumber(n.Y);
                int step = _timeCalc.GetStepIndex(n.Y, 240);

                if (n.L > 0 && n.X > 0)
                {
                    string lnChannel = MapLaneToChannel(n.X, true);
                    long endY = n.Y + n.L;
                    int endMeasure = _timeCalc.GetMeasureNumber(endY);
                    int endStep = _timeCalc.GetStepIndex(endY, 240);

                    // LNの開始と終了をLNチャンネルに配置
                    AddNote(measure, lnChannel, step, wavId);
                    AddNote(endMeasure, lnChannel, endStep, wavId);
                }
                else
                {
                    AddNote(measure, bmsChannel, step, wavId);
                }
            }
        });
    }

    private void ProcessBpmEvents()
    {
        if (_bmson.BpmEvents == null) return;
        foreach (var b in _bmson.BpmEvents)
        {
            if (!_bpmDefinitions.TryGetValue(b.Bpm, out string? bpmId))
            {
                bpmId = RadixConvert.IntToZZ(_bpmCounter++, 36); // #BPMxx は通常36進数
                _bpmDefinitions[b.Bpm] = bpmId;
            }

            int m = _timeCalc.GetMeasureNumber(b.Y);
            int step = _timeCalc.GetStepIndex(b.Y, 240);
            AddNote(m, "08", step, bpmId);
        }
    }

    private void ProcessStopEvents()
    {
        if (_bmson.StopEvents == null) return;
        foreach (var s in _bmson.StopEvents)
        {
            // ストップ時間は、BMSのストップ定義では 4/4小節を 192 とした場合の値
            // つまり 192 = 1小節
            // Bmsonの Duration は単なるパルス数。
            // Resolution (R) が与えられている場合、1拍 = R パルス。4/4小節 = 4R パルス。
            // BMS STOP = (Duration / 4R) * 192 = (Duration * 48) / R
            long bmsStopVal = (s.Duration * 48) / _bmson.Info.Resolution;

            if (!_stopDefinitions.TryGetValue(bmsStopVal, out string? stopId))
            {
                stopId = RadixConvert.IntToZZ(_stopCounter++, 36);
                _stopDefinitions[bmsStopVal] = stopId;
            }

            int m = _timeCalc.GetMeasureNumber(s.Y);
            int step = _timeCalc.GetStepIndex(s.Y, 240);
            AddNote(m, "09", step, stopId);
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
                _bmpDefinitions[h.Id] = RadixConvert.IntToZZ(_bmpCounter++, 36);
            }
        }

        void AddBgaEvents(List<BmsonBgaEvent> events, string channel)
        {
            if (events == null) return;
            foreach (var e in events)
            {
                if (_bmpDefinitions.TryGetValue(e.Id, out string? bmpId))
                {
                    int m = _timeCalc.GetMeasureNumber(e.Y);
                    int step = _timeCalc.GetStepIndex(e.Y, 240);
                    AddNote(m, channel, step, bmpId);
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
                // 小数で表現
                string multStr = mult.ToString("0.000000").TrimEnd('0').TrimEnd('.');
                // 02チャンネルは文字列として登録する特別な扱い
                if (!_measures.ContainsKey(m)) _measures[m] = [];
                if (!_measures[m].ContainsKey("02")) _measures[m]["02"] = [[multStr]];
            }
        }
    }

    private string GetWavId(string fileName)
    {
        lock (_wavDefinitions)
        {
            if (!_wavDefinitions.TryGetValue(fileName, out string? id))
            {
                id = RadixConvert.IntToZZ(_wavCounter++, _radix);
                _wavDefinitions[fileName] = id;
            }
            return id;
        }
    }

    private static string[] CreateEmptyArray(int size = 240)
    {
        var arr = new string[size];
        for (int i = 0; i < size; i++) arr[i] = "00";
        return arr;
    }

    private void AddNote(int measure, string channel, int step, string id)
    {
        lock (_measures)
        {
            if (!_measures.ContainsKey(measure)) _measures[measure] = [];
            var mDict = _measures[measure];

            if (!mDict.ContainsKey(channel)) mDict[channel] = [CreateEmptyArray()];

            if (channel == "01")
            {
                bool placed = false;
                foreach (var arr in mDict[channel])
                {
                    if (arr[step] == "00")
                    {
                        arr[step] = id;
                        placed = true;
                        break;
                    }
                }
                if (!placed)
                {
                    var newArr = CreateEmptyArray();
                    newArr[step] = id;
                    mDict[channel].Add(newArr);
                }
            }
            else
            {
                mDict[channel][0][step] = id;
            }
        }
    }

    private static string MapLaneToChannel(int x, bool isLn)
    {
        if (x == 0) return "01";
        // 1P
        if (x == 1) return isLn ? "51" : "11";
        if (x == 2) return isLn ? "52" : "12";
        if (x == 3) return isLn ? "53" : "13";
        if (x == 4) return isLn ? "54" : "14";
        if (x == 5) return isLn ? "55" : "15";
        if (x == 6) return isLn ? "58" : "18";
        if (x == 7) return isLn ? "59" : "19";
        if (x == 8) return isLn ? "56" : "16";
        // 2P
        if (x == 9) return isLn ? "61" : "21";
        if (x == 10) return isLn ? "62" : "22";
        if (x == 11) return isLn ? "63" : "23";
        if (x == 12) return isLn ? "64" : "24";
        if (x == 13) return isLn ? "65" : "25";
        if (x == 14) return isLn ? "68" : "28";
        if (x == 15) return isLn ? "69" : "29";
        if (x == 16) return isLn ? "66" : "26";

        return "01";
    }
}
