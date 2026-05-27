using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// bmsonの絶対パルス(y)を実時間(秒)に変換する計算機。
/// BPM変更やストップイベントを考慮した連続的な時間マッピングを提供します。
/// </summary>
public class PulseToRealTimeCalculator
{
    private readonly int _resolution;
    private readonly List<TimeSegment> _segments = [];

    private class TimeSegment
    {
        public long StartY { get; set; }
        public double StartTimeSec { get; set; }
        public double Bpm { get; set; }
        public bool IsAfterStop { get; set; } = false;
    }

    public PulseToRealTimeCalculator(int resolution, double initBpm, List<BmsonBpmEvent>? bpmEvents, List<BmsonStopEvent>? stopEvents)
    {
        _resolution = resolution <= 0 ? 240 : resolution;
        BuildSegments(initBpm, bpmEvents ?? [], stopEvents ?? []);
    }

    private void BuildSegments(double initBpm, List<BmsonBpmEvent> bpmEvents, List<BmsonStopEvent> stopEvents)
    {
        var events = bpmEvents.Select(static b => (b.Y, Type: 0, Value: (double)b.Bpm))
            .Concat(stopEvents.Select(static s => (s.Y, Type: 1, Value: (double)s.Duration)))
            .OrderBy(static e => e.Y)
            .ThenBy(static e => e.Type)
            .ToList();

        long currentY = 0;
        double currentTimeSec = 0.0;
        double currentBpm = initBpm;

        // 最初のセグメントを追加
        _segments.Add(new TimeSegment
        {
            StartY = currentY,
            StartTimeSec = currentTimeSec,
            Bpm = currentBpm
        });

        foreach (var ev in events)
        {
            if (ev.Y > currentY)
            {
                // currentY から ev.Y までの時間を計算して進める
                currentTimeSec += PulsesToSeconds(ev.Y - currentY, currentBpm);
                currentY = ev.Y;
            }

            if (ev.Type == 0) // BPM
            {
                currentBpm = ev.Value;
                // 新しいBPMのセグメントを追加
                _segments.Add(new TimeSegment
                {
                    StartY = currentY,
                    StartTimeSec = currentTimeSec,
                    Bpm = currentBpm
                });
            }
            else if (ev.Type == 1) // STOP
            {
                // STOPイベントはYを進めずに時間だけを進める
                double stopDurationSec = PulsesToSeconds((long)ev.Value, currentBpm);
                currentTimeSec += stopDurationSec;

                // "y <= StartY" の場合はそのセグメントの直前までの計算で良い。
                // そのため、ここでは StartY を currentY にしてセグメントを追加するが、
                // 検索時に工夫する。

                _segments.Add(new TimeSegment
                {
                    StartY = currentY, // 厳密にはここから時間がジャンプする
                    StartTimeSec = currentTimeSec,
                    Bpm = currentBpm,
                    IsAfterStop = true
                });
            }
        }
    }


    /// <summary>
    /// 指定されたパルス数を現在のBPMでの秒数に変換します。
    /// </summary>
    private double PulsesToSeconds(long pulses, double bpm)
    {
        if (bpm <= 0) return 0;
        return (double)pulses * 60.0 / (bpm * _resolution);
    }

    /// <summary>
    /// 絶対パルス(y)に対応する実時間(秒)を取得します。
    /// </summary>
    public double GetTimeSec(long y)
    {
        if (_segments.Count == 0) return 0;

        // y以下のStartYを持つ最新のセグメントを二分探索で探す
        var targetSegment = FindSegment(y);

        long diffPulses = y - targetSegment.StartY;
        double diffSec = PulsesToSeconds(diffPulses, targetSegment.Bpm);

        return targetSegment.StartTimeSec + diffSec;
    }

    private TimeSegment FindSegment(long y)
    {
        int low = 0;
        int high = _segments.Count - 1;
        int ans = 0;

        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            if (_segments[mid].StartY <= y)
            {
                ans = mid;
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        if (_segments[ans].StartY == y)
        {
            int i = ans;
            while (i >= 0 && _segments[i].StartY == y)
            {
                if (!_segments[i].IsAfterStop)
                {
                    return _segments[i];
                }
                i--;
            }
            if (i >= 0)
            {
                ans = i;
            }
        }

        return _segments[ans];
    }
}
