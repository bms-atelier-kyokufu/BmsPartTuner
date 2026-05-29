namespace BmsAtelierKyokufu.BmsPartTuner.Core.Audio;

/// <summary>
/// 有音区間の重なりを計算する際の中間累積統計量を保持するスタック専用構造体。
/// </summary>
public ref struct PearsonOverlapStats
{
    /// <summary>積和（ΣXY）</summary>
    public double TotalDotProduct;

    /// <summary>波形1の累積和（ΣX）</summary>
    public double TotalSumX;

    /// <summary>波形2の累積和（ΣY）</summary>
    public double TotalSumY;

    /// <summary>波形1の自乗累積和（ΣX²）</summary>
    public double TotalSumX2;

    /// <summary>波形2の自乗累積和（ΣY²）</summary>
    public double TotalSumY2;

    /// <summary>累積サンプル数</summary>
    public int TotalN;
}
