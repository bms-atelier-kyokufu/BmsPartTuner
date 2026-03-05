# 汎用変異テストフレームワーク

Roslynを使用した、プロジェクト独自の変異テストフレームワークです。

## ディレクトリ構成

```
RoslynMutation/
├── Core/
│   └── MutationTypes.cs          # 列挙型とレコード定義
├── Config/
│   └── MutationTestConfiguration.cs  # テスト設定
├── TestCases/
│   └── IMutantTestCase.cs        # テストケースインターフェース
├── Reporting/
│   └── MutationTestReport.cs     # レポートDTO
├── Generation/
│   └── MutationGenerator.cs      # 変異生成ロジック
├── Compilation/
│   └── MutationCompiler.cs       # コンパイラ
├── Execution/
│   └── MutationTestRunner.cs     # テスト実行ランナー
└── README.md                      # このファイル
```

**注意:** すべてのクラスは同じ名前空間 `BmsAtelierKyokufu.BmsPartTuner.Tests.Mutation` を使用しています。
ディレクトリは論理的な分類のためだけで、名前空間は統一されているため、`using` ディレクティブは不要です。

## 使用方法

### Fluent API（推奨）

```csharp
// シンプルな使い方
var report = MutationTestRunner
    .Create()
    .Configure(projectName: "MyProject", markerDirectory: "Core")
    .RunAll();

// カスタムテストケースとロガーを追加
var report = MutationTestRunner
    .Create()
    .Configure(projectName: "MyProject", markerDirectory: "Core")
    .WithTestCase(new CalculatorTestCase())
    .WithTestCase(new StringUtilsTestCase())
    .WithLogger(msg => Console.WriteLine(msg))
    .RunAll();

// 詳細なカスタマイズ
var report = MutationTestRunner
    .Create()
    .Configure(projectName: "MyProject")
    .WithTestCase(new MyTestCase())
    .WithLogger(Console.WriteLine)
    .WithMaxParallelism(4)
    .IncludeFiles("Core/", "Services/")
    .ExcludeFiles("Legacy/")
    .DisableJsonOutput()
    .RunAll();

// 特定ファイルのみテスト
var report = MutationTestRunner
    .Create()
    .Configure(projectName: "MyProject")
    .WithTestCase(new CalculatorTestCase())
    .RunForFile("Core/Helpers/Calculator.cs");
```

### 従来の方法（互換性のため残存）

```csharp
// 1. 設定を作成（プロジェクト名から自動検出）
var config = MutationTestConfiguration.AutoDetect(
    projectName: "MyProject",
    markerDirectory: "Core"
);

// 2. カスタムテストケースを登録（オプション）
var registry = new MutantTestCaseRegistry();
registry.Register(new MyClassTestCase());

// 3. ランナーを作成して実行
var runner = new MutationTestRunner(config, registry, Console.WriteLine);
var report = runner.RunAll();

// 4. 結果を確認
Console.WriteLine($"変異スコア: {report.MutationScore:F1}%");
```

### カスタムテストケースの作成

```csharp
public class CalculatorTestCase : IMutantTestCase
{
    public string TypeName => "Calculator";
    
    public bool TestMutant(Assembly assembly)
    {
        var type = assembly.GetType("MyApp.Calculator");
        if (type == null) return true;
        
        var addMethod = type.GetMethod("Add", new[] { typeof(int), typeof(int) });
        var result = (int)addMethod.Invoke(null, new object[] { 2, 3 });
        
        // 期待値と異なる場合は true (Killed)
        return result != 5;
    }
}
```

## サポートする変異パターン

| カテゴリ | 変異パターン |
|---------|------------|
| 等価演算子 | `==` ? `!=` |
| 比較演算子 | `<` ? `<=`, `>` ? `>=`, `<` ? `>`, `<=` ? `>=` |
| 論理演算子 | `&&` ? `||` |
| 算術演算子 | `+` ? `-`, `*` ? `/` |
| ブール定数 | `true` ? `false` |
| 数値リテラル | `n` → `n+1`, `n` → `n-1`, `n` → `0` |
| LINQ | `First` ? `Last`, `Any` ? `All`, `Take` ? `Skip`<br>`OrderBy` ? `OrderByDescending`, `Min` ? `Max`<br>`Sum` ? `Count`, `Where`条件反転 など |

## レポート出力

### コンソール出力

```
========================================
=== 変異テスト結果サマリー ===
========================================
 実行時間: 45.2秒
総変異数: 245
? Killed: 198 (80.8%)
  ├─ コンパイルエラー: 12
  └─ テスト実行で検出: 186
? Survived: 47 (19.2%)
変異スコア: 80.8%
========================================
```

### JSON出力

`TestResults/MutationTest_20240101_123456.json` に詳細な結果が保存されます。

## カスタマイズ

### Fluent API でのカスタマイズ

```csharp
var report = MutationTestRunner
    .Create()
    .Configure(projectName: "MyProject")
    
    // 並列度の調整
    .WithMaxParallelism(4)
    
    // 特定ディレクトリのみを対象
    .IncludeFiles("Core/", "Services/")
    
    // 追加の除外パターン
    .ExcludeFiles("Legacy/", "Deprecated/")
    
    // JSON出力の無効化
    .DisableJsonOutput()
    
    // カスタムロガー
    .WithLogger(msg => Debug.WriteLine(msg))
    
    // 実行
    .RunAll();
```

### 従来の方法でのカスタマイズ

```csharp
var config = MutationTestConfiguration.AutoDetect("MyProject");

// 並列度の調整
config.MaxParallelism = 4;

// 特定ディレクトリのみを対象
config.IncludePatterns.Add("Core/");
config.IncludePatterns.Add("Services/");

// 追加の除外パターン
config.ExcludePatterns.Add("Legacy/");

// JSON出力の無効化
config.SaveResultsToJson = false;

// 追加のアセンブリ参照
config.AdditionalReferences.Add(
    MetadataReference.CreateFromFile(typeof(MyLibrary).Assembly.Location)
);
```

## 用語解説

- **Mutant (変異体)**: ソースコードに1つの変更を加えたもの
- **Killed (検出)**: テストが変異を検出して失敗した（良いテスト）
- **Survived (生存)**: テストが変異を検出できなかった（改善が必要）
- **Mutation Score (変異スコア)**: `Killed / Total × 100%` （高いほど良い、80%以上が目標）

## トラブルシューティング

### ソースディレクトリが見つからない

```csharp
// Fluent API
var config = new MutationTestConfiguration
{
    SourceDirectory = Path.GetFullPath("../../../MyProject")
};

var report = MutationTestRunner
    .Create()
    .Configure(config)
    .RunAll();

// または従来の方法
var config = new MutationTestConfiguration
{
    SourceDirectory = Path.GetFullPath("../../../MyProject")
};
var runner = new MutationTestRunner(config);
var report = runner.RunAll();
```

### コンパイルエラーが多発する

```csharp
// 注意: 現在のFluent APIでは AdditionalReferences の設定は
// カスタム設定を経由する必要があります
var config = MutationTestConfiguration.AutoDetect("MyProject");
config.AdditionalReferences.Add(
    MetadataReference.CreateFromFile(Assembly.Load("System.Xml").Location)
);

var report = MutationTestRunner
    .Create()
    .Configure(config)
    .RunAll();
```

### 実行が遅い

```csharp
// Fluent API で並列度を増やす
var report = MutationTestRunner
    .Create()
    .Configure(projectName: "MyProject")
    .WithMaxParallelism(Environment.ProcessorCount)
    .RunAll();

// または特定ファイルのみテスト
var report = MutationTestRunner
    .Create()
    .Configure(projectName: "MyProject")
    .RunForFile("Core/Helpers/Calculator.cs");
```

## 設計思想

### アーキテクチャ

- **Why Roslyn?**: Stryker.NETなどの外部ツールに依存せず、複雑なビルド構成（WPF、.NET 10等）に対応
- **Why メモリコンパイル?**: ディスクI/Oを避けて高速化、並列処理で複数の変異を同時にテスト
- **Why CollectibleAssemblyLoadContext?**: 数百?数千のアセンブリをロードするため、メモリリークを防止
- **Why カスタムテストケース?**: 汎用テストでは引数なしメソッドしか実行できないため、複雑なロジックに対応

### 名前空間設計

すべてのクラスは単一の名前空間 `BmsAtelierKyokufu.BmsPartTuner.Tests.Mutation` を使用します。

```
? 単一の名前空間
BmsAtelierKyokufu.BmsPartTuner.Tests.Mutation
├── MutationTypes (列挙型・レコード)
├── MutationTestConfiguration (設定)
├── IMutantTestCase (インターフェース)
├── MutationTestReport (レポート)
├── MutationGenerator (生成)
├── MutationCompiler (コンパイル)
└── MutationTestRunner (実行)
```

**Why 単一名前空間?**
- `using` ディレクティブが不要
- すべての型が同じスコープでアクセス可能
- シンプルで直感的なAPI
- ディレクトリ構造は論理的な分類のため（物理的な整理）

### Fluent API

メソッドチェーンによる設定で、可読性と保守性を向上:

```csharp
MutationTestRunner
    .Create()                    // ビルダー開始
    .Configure(...)              // 設定
    .WithTestCase(...)           // テストケース追加
    .WithLogger(...)             // ロガー設定
    .RunAll();                   // 実行
```

## ライセンス

このフレームワークはプロジェクト内部で使用するためのものです。
