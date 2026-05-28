using System;

namespace BmsAtelierKyokufu.BmsPartTuner.Core.Attributes;

/// <summary>
/// ADR (Architecture Decision Record) とコードを紐づけるための属性。
/// ドキュメント側のメタデータとソースコードの追従性を高める目的で使用します。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = true)]
public class ADRAnchorAttribute : Attribute
{
    public string Id { get; }
    public string TargetName { get; }

    public ADRAnchorAttribute(string id, string targetName)
    {
        Id = id;
        TargetName = targetName;
    }
}
