using BmsAtelierKyokufu.BmsPartTuner.Models.Bmson;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Bms.Bmson;

/// <summary>
/// パースしたbmsonデータに対して、数学的モデルに合わせた制約の保証や不正値のクリーンアップを行う。
/// </summary>
public static class BmsonSanitizer
{
    /// <summary>
    /// bmsonデータをサニタイズし、変換に必要な前提条件を保証します。
    /// </summary>
    /// <param name="bmson">サニタイズ対象のbmsonデータ。</param>
    public static BmsonFormat Sanitize(BmsonFormat bmson)
    {
        if (bmson == null)
            return bmson!;

        // Lines（小節線）の制約: 最初の小節線は y = 0 でなければならない
        // bmsonの仕様上省略可能だが、BMS変換の数学モデル（小節番号・相対位置算出）では必須
        var lines = bmson.Lines?.ToList() ?? [];

        if (lines.Count == 0 || lines[0].Y > 0)
        {
            lines.Insert(0, new BmsonLineEvent { Y = 0 });
        }

        // yが負の小節線や、順序が逆転しているものを整理（昇順ソート）し、重複を排除
        var uniqueLines = lines
            .OrderBy(static l => l.Y)
            .DistinctBy(static l => l.Y)
            .ToList();

        // ノーツをy座標でソートする（音声切り出しの前提条件）
        var channels = bmson.SoundChannels?.ToList() ?? [];
        for (int i = 0; i < channels.Count; i++)
        {
            if (channels[i].Notes != null)
            {
                var sortedNotes = channels[i].Notes.OrderBy(static n => n.Y).ToList();
                channels[i] = channels[i] with { Notes = sortedNotes };
            }
        }

        return bmson with
        {
            Lines = uniqueLines,
            SoundChannels = channels
        };
    }
}
