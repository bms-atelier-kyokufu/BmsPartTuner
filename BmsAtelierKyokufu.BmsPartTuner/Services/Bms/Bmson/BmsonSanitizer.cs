using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

namespace BmsAtelierKyokufu.BmsPartTuner.Services.Bms.Bmson;

/// <summary>
/// パースしたbmsonデータに対して、数学的モデルに合わせた制約の保証や不正値のクリーンアップを行う。
/// </summary>
public static class BmsonSanitizer
{
    /// <summary>
    /// bmsonデータをサニタイズし、変換に必要な前提条件を保証します。
    /// </summary>
    /// <param name="bmson">サニタイズ対象のbmsonデータ。</param>
    public static void Sanitize(BmsonFormat bmson)
    {
        if (bmson == null)
            return;

        // Lines（小節線）の制約: 最初の小節線は y = 0 でなければならない
        // bmsonの仕様上省略可能だが、BMS変換の数学モデル（小節番号・相対位置算出）では必須
        bmson.Lines ??= [];

        if (bmson.Lines.Count == 0 || bmson.Lines[0].Y > 0)
        {
            bmson.Lines.Insert(0, new BmsonLineEvent { Y = 0 });
        }

        // yが負の小節線や、順序が逆転しているものを整理（昇順ソート）
        bmson.Lines = [.. bmson.Lines.OrderBy(l => l.Y)];

        // 不要な重複小節線（同じy座標）を排除
        List<BmsonLineEvent> uniqueLines = [];
        long lastY = -1;
        foreach (var line in bmson.Lines)
        {
            if (line.Y != lastY)
            {
                uniqueLines.Add(line);
                lastY = line.Y;
            }
        }
        bmson.Lines = uniqueLines;

        // ノーツをy座標でソートする（音声切り出しの前提条件）
        if (bmson.SoundChannels != null)
        {
            foreach (var channel in bmson.SoundChannels)
            {
                if (channel.Notes != null)
                {
                    channel.Notes = [.. channel.Notes.OrderBy(n => n.Y)];
                }
            }
        }
    }
}
