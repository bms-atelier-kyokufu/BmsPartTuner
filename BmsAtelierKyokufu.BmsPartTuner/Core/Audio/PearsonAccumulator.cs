namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// ピアソン相関係数の計算に必要な中間累積統計量を保持するスタック専用構造体。
/// </summary>
public ref struct PearsonAccumulator
{
    /// <summary>波形1の総和（ΣX）</summary>
    public float SumX;

    /// <summary>波形2の総和（ΣY）</summary>
    public float SumY;

    /// <summary>波形1の自乗和（ΣX²）</summary>
    public float SumX2;

    /// <summary>波形2の自乗和（ΣY²）</summary>
    public float SumY2;

    /// <summary>波形1と波形2の積の総和（ΣXY）</summary>
    public float SumXY;
}
