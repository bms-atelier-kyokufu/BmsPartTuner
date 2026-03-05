using System.Reflection;
using System.Text;
using BmsAtelierKyokufu.BmsPartTuner.Models;

namespace BmsAtelierKyokufu.BmsPartTuner.Services;

/// <summary>
/// 埋め込まれたライセンスファイルを読み込むサービス。
/// </summary>
public class LicenseLoaderService
{
    private const string LicenseResourcePath = "BmsAtelierKyokufu.BmsPartTuner.Resources.Licenses";

    /// <summary>
    /// 全てのライセンス情報を読み込みます。
    /// </summary>
    /// <returns>ライセンス情報のリスト。自身のライセンスが先頭になります。</returns>
    public IEnumerable<LicenseInfo> LoadLicenses()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        string[] resourceNames = assembly.GetManifestResourceNames();
        List<LicenseInfo> licenses = new List<LicenseInfo>();

        foreach (string resourceName in resourceNames)
        {
            if (!resourceName.StartsWith(LicenseResourcePath) || !resourceName.EndsWith(".md"))
            {
                continue;
            }

            string content = ReadResource(assembly, resourceName);
            string fileName = GetFileNameFromResourceName(resourceName);
            bool isAppLicense = fileName.Equals("AppLicense", StringComparison.OrdinalIgnoreCase);

            licenses.Add(new LicenseInfo
            {
                Name = isAppLicense ? "Bms Part Tuner" : fileName,
                Content = content,
                IsAppLicense = isAppLicense
            });
        }

        return licenses
            .OrderByDescending(x => x.IsAppLicense)
            .ThenBy(x => x.Name);
    }

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null) return string.Empty;

        using StreamReader reader = new StreamReader(stream, Encoding.UTF8);
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
            nameWithoutExt = nameWithoutExt.Substring(LicenseResourcePath.Length);
        }

        // 3. 先頭のドットを除去 (例: .AppLicense -> AppLicense)
        if (nameWithoutExt.StartsWith("."))
        {
            nameWithoutExt = nameWithoutExt.Substring(1);
        }

        // 4. ThirdPartyフォルダ内にある場合は、そのプレフィックスも除去
        // リソース名では "ThirdParty." となっているはず
        const string thirdPartyPrefix = "ThirdParty.";
        if (nameWithoutExt.StartsWith(thirdPartyPrefix))
        {
            nameWithoutExt = nameWithoutExt.Substring(thirdPartyPrefix.Length);
        }

        return nameWithoutExt;
    }
}
