using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Bms;

/// <summary>
/// bmsonの絶対パルス(y)をBMSの小節(Measure)・相対位置・ステップインデックスに変換する計算機。
/// </summary>
public class PulseToBmsTimeCalculator(int resolution, List<BmsonLineEvent> lines)
{
    private readonly int _resolution = resolution <= 0 ? 240 : resolution;
    private readonly List<BmsonLineEvent> _lines = lines ?? [];

    /// <summary>
    /// yパルスが属する小節番号(m)を取得します。
    /// </summary>
    public int GetMeasureNumber(long y)
    {
        if (_lines.Count == 0) return 0;
        if (y < _lines[0].Y) return 0;

        // 二分探索で l_m <= y を満たす最大のインデックスを見つける
        int left = 0;
        int right = _lines.Count - 1;
        int m = 0;

        while (left <= right)
        {
            int mid = left + (right - left) / 2;
            if (_lines[mid].Y <= y)
            {
                m = mid;
                left = mid + 1;
            }
            else
            {
                right = mid - 1;
            }
        }
        return m;
    }

    /// <summary>
    /// 指定した小節の長さをパルス単位で取得します。
    /// </summary>
    public long GetMeasureLength(int measureIndex)
    {
        if (measureIndex < 0) return 4 * _resolution;

        long currentY = measureIndex < _lines.Count ? _lines[measureIndex].Y : measureIndex * 4 * _resolution;
        long nextY;

        if (measureIndex + 1 < _lines.Count)
        {
            nextY = _lines[measureIndex + 1].Y;
        }
        else
        {
            // 次の小節線がない場合は、デフォルトの小節長（4/4拍子）を仮定
            nextY = currentY + 4 * _resolution;
        }

        return nextY - currentY;
    }

    /// <summary>
    /// yパルスの小節内の相対位置（0.0 〜 1.0未満）を取得します。
    /// </summary>
    public double GetRelativePosition(long y)
    {
        int m = GetMeasureNumber(y);
        long mStart = m < _lines.Count ? _lines[m].Y : m * 4 * _resolution;
        long length = GetMeasureLength(m);

        if (length == 0) return 0.0;
        return (double)(y - mStart) / length;
    }

    /// <summary>
    /// 小節の長さのスケール値（Mm）を取得します。
    /// 標準の4/4小節長（4 * Resolution）に対する比率です。（BMSの #xxx02 に出力する値）
    /// </summary>
    public double GetMeterMultiplier(int measureIndex)
    {
        long length = GetMeasureLength(measureIndex);
        return (double)length / (4 * _resolution);
    }

    /// <summary>
    /// オラクルの変換ロジックに基づき、指定した出力ステップ数に対するステップインデックスを取得します。
    /// step = Floor( (y - y_start) * R / (y_end - y_start) )
    /// ここで、R は出力ステップ数。
    /// </summary>
    public int GetStepIndex(long y, int outputSteps)
    {
        int m = GetMeasureNumber(y);
        long mStart = m < _lines.Count ? _lines[m].Y : m * 4 * _resolution;
        long length = GetMeasureLength(m);

        if (length == 0) return 0;

        // 小数点以下の誤差を避けるため、doubleではなく分数で計算して切り捨てる
        long step = (y - mStart) * outputSteps / length;

        // y == y_end の場合の安全策
        if (step >= outputSteps)
        {
            step = outputSteps - 1;
        }
        return (int)step;
    }
}
