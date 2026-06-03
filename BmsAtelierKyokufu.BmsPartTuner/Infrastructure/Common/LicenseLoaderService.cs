using System.Reflection;
using System.Text.RegularExpressions;

namespace BmsAtelierKyokufu.BmsPartTuner.Infrastructure.Common;

/// <summary>
/// 埋め込まれたライセンスファイルを読み込むサービス。
/// </summary>
[ADRAnchor("ARCH-04", nameof(LicenseLoaderService))]
public partial class LicenseLoaderService
{
    /// <summary>
    /// サービスインスタンスの一意の識別子。
    /// </summary>
    public string InstanceId { get; } = Guid.NewGuid().ToString();

    private const string LicenseResourcePath = "BmsAtelierKyokufu.BmsPartTuner.Resources.Licenses";

    /// <summary>
    /// 全てのライセンス情報を読み込みます。
    /// </summary>
    /// <returns>ライセンス情報のリスト。自身のライセンスが先頭になります。</returns>
    public static IEnumerable<LicenseInfo> LoadLicenses()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string[] resourceNames = assembly.GetManifestResourceNames();
        List<LicenseInfo> licenses = [];

        foreach (string resourceName in resourceNames)
        {
            if (!resourceName.StartsWith(LicenseResourcePath) || !resourceName.EndsWith(".md") || resourceName.Contains(".Templates."))
            {
                continue;
            }

            string content = ReadResource(assembly, resourceName);
            string fileName = GetFileNameFromResourceName(resourceName);
            bool isAppLicense = fileName.Equals("AppLicense", StringComparison.OrdinalIgnoreCase);
            bool isUnique = true;

            // テンプレート指定の解析: {{Templates/MIT.md, Copyright (c) ...}}
            // e.g. {{Templates/MIT.md, Copyright (c) 2015 Kristian Hellang}}
            var match = LicensePlaceholderRegex().Match(content);
            if (match.Success)
            {
                isUnique = false;
                string targetFileName = match.Groups[1].Value.Replace('/', '.').Replace('\\', '.').Replace(',', '.').Trim();
                string copyrightText = match.Groups[2].Value.Trim();
                string targetNameWithoutExt = Path.GetFileNameWithoutExtension(targetFileName);

                string? targetResourceName = resourceNames.FirstOrDefault(r =>
                    r.StartsWith(LicenseResourcePath) &&
                    r.Contains(targetNameWithoutExt, StringComparison.OrdinalIgnoreCase));

                if (targetResourceName != null)
                {
                    string templateContent = ReadResource(assembly, targetResourceName);
                    content = templateContent
                        .Replace("# MIT License", $"# {fileName} License")
                        .Replace("[Copyright]", copyrightText);
                }
            }

            string displayName = isAppLicense ? "Bms Part Tuner" : fileName;
            if (!isAppLicense && isUnique)
            {
                displayName += " *";
            }

            licenses.Add(new LicenseInfo
            {
                Name = displayName,
                Content = content,
                IsAppLicense = isAppLicense
            });
        }

        return licenses
            .OrderByDescending(static x => x.IsAppLicense)
            .ThenBy(static x => x.Name);
    }

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return string.Empty;

        using StreamReader reader = new(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string GetFileNameFromResourceName(string resourceName)
    {
        // リソース名: BmsAtelierKyokufu.BmsPartTuner.Resources.Licenses.ThirdParty.Microsoft.Extensions.Hosting.md
        // または: BmsAtelierKyokufu.BmsPartTuner.Resources.Licenses.AppLicense.md

        // 1. 拡張子(.md)を除去
        string nameWithoutExt = Path.GetFileNameWithoutExtension(resourceName);

        // 2. プレフィックス(Namespace + Path)を除去
        if (nameWithoutExt.StartsWith(LicenseResourcePath))
        {
            nameWithoutExt = nameWithoutExt[LicenseResourcePath.Length..];
        }

        // 3. 先頭のドットを除去 (例: .AppLicense -> AppLicense)
        if (nameWithoutExt.StartsWith('.'))
        {
            nameWithoutExt = nameWithoutExt[1..];
        }

        // 4. ThirdPartyフォルダ内にある場合は、そのプレフィックスも除去
        // リソース名では "ThirdParty." となっているはず
        const string thirdPartyPrefix = "ThirdParty.";
        if (nameWithoutExt.StartsWith(thirdPartyPrefix))
        {
            nameWithoutExt = nameWithoutExt[thirdPartyPrefix.Length..];
        }

        return nameWithoutExt;
    }

    [GeneratedRegex(@"\{\{\s*([^,\s}]+)\s*,\s*([^}]+)\}\}")]
    private static partial Regex LicensePlaceholderRegex();
}
