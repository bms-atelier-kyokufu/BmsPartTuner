namespace BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;

/// <summary>
/// ADR (Architecture Decision Record) とコードを紐づけるための属性。
/// ドキュメント側のメタデータとソースコードの追従性を高める目的で使用します。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Interface, AllowMultiple = true)]
public class ADRAnchorAttribute(string id, string targetName) : Attribute
{
    public string Id { get; } = id;
    public string TargetName { get; } = targetName;
}
