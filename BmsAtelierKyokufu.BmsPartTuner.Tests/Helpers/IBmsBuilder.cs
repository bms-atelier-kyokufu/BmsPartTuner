namespace BmsAtelierKyokufu.BmsPartTuner.Tests.Helpers
{
    /// <summary>
    /// BMS および BMSON 形式の譜面データを流れるように構築するための共通インターフェース。
    /// </summary>
    public interface IBmsFamilyBuilder
    {
        IBmsFamilyBuilder WithHeader(string key, string value);
        IBmsFamilyBuilder WithWav(int index, string filename, bool createFile = true, bool writeToDisk = true);
        IBmsFamilyBuilder WithWav(string indexStr, string filename, bool createFile = true, bool writeToDisk = true);
        IBmsFamilyBuilder AddMainData(int measure, int channel, string data);
        IBmsFamilyBuilder AddMainData(int channel, string data);
    }

    /// <summary>
    /// 型安全にビルダーインスタンスを作成できる、ジェネリック対応の共通インターフェース。
    /// </summary>
    public interface IBmsFamilyBuilder<TSelf> : IBmsFamilyBuilder where TSelf : class, IBmsFamilyBuilder<TSelf>
    {
        static abstract TSelf Create(BmsFamilyTestContext context);
    }
}
