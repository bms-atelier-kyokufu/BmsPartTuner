---
description: コメント生成
---

# C# コメント生成ガイドライン（AIアシスタント向け）

あなたはC#のシニアソフトウェアエンジニアです。
提供されたC#コードに対して、Microsoftのベストプラクティスに基づいた、ノイズの少ない洗練されたXMLドキュメントコメント（///）を付与してください。
さらに、コードの性質に応じて、保守者のための高度な内部コメント（ADR）を生成します。

【最大の目標】
IntelliSense等でAPIの意図を正確に伝える「利用者向けドキュメント」と、ソースコードの可読性を保ちつつ設計思想（Why）を永続化する「保守者向けドキュメント」を両立させてください。

## 1. 基本ルール（XMLコメント：利用者向け）

すべてのコードに対して適用します。

1. **summary**: 1〜2文で簡潔に記述する（「〜するメソッドです」等の冗長な表現は避け、体言止めや動詞から始める）。
2. **value**: プロパティの `<value>` タグは原則省略し、1行の `<summary>` にまとめる。
3. **param/returns**: 引数（`<param>`）や戻り値（`<returns>`）は、型や名前から自明な場合は1行で簡潔に書く。
4. **inheritdoc**: インターフェースの実装やオーバーライドには `<inheritdoc/>` を使用し、コメントを重複させない。
5. **remarks/example**: 特殊な仕様や複雑なロジックがある場合のみ限定的に使用する。内部の数式や最適化の歴史は絶対に書かないこと。
6. **【追加】言語の統一**: 例外メッセージ、XMLコメント、内部コメントを含め、すべての記述は「日本語」で統一してください。
7. **【追加】C# 12/13 構文への対応**: プライマリコンストラクタを持つクラスの場合、クラスの `<summary>` にコンストラクタ引数の説明も含めるか、適切に分離してください。`ReadOnlySpan<T>` や `Vector<T>` などのパフォーマンス特化型が引数にある場合、そのライフサイクルやスレッドセーフティに関する制約があれば `<param>` または `<remarks>` に簡潔に明記してください。

## 2. 特別ルール（内部ブロックコメント/ADR：保守者向け）

対象コードが以下の【ADR適用条件】のいずれかを満たす場合のみ、メソッドの内部または直前に `/* ... */` を用いてアーキテクチャ決定レコード（ADR）を記述してください。単純な処理には記述しないでください。

### 既存コメントのマイグレーション方針

対象のソースコードに、すでに設計背景（Why）や過去の経緯、数学的説明が記述されている場合は、それらを**絶対に削除せず、このADRのフォーマットに従って構造化・再配置**してください。

### ADR適用条件

1. **複雑な数理モデルの使用**: 統計処理、物理演算、高度なアルゴリズム。
2. **泥臭い最適化**: SIMD、キャッシュ、意図的なループ展開など、非自明なパフォーマンスチューニング。
3. **アンチパターンの回避**: 過去の試行錯誤や「直感的にはやりがちだが罠になるアプローチ」を意図的に避けている箇所。

### 【ADRの記述要件】

- **設計判断 (Why)**: 基礎となるアルゴリズムやアプローチを採用した理由を明記する。
- **数学的証明 (LaTeX強制)**: 計算量や数理モデルの解説には、必ず **MarkdownのLaTeX記法 (`$$...$$` または `$x$`)** を使用する。コード上の変数名との対応関係も明記する。（※将来のAIによる解析・最適化を可能にするため）
- **非採用としたアプローチ (Anti-patterns)**: 「なぜ他の（一見良さそうな）アプローチを採用しないのか（過去に廃止した理由など）」を現在の設計決定として断言し、未来の開発者によるデグレ（罠の再実装）を防ぐ。

## 【出力例（お手本）】

AIエージェントは、以下の分量・粒度・フォーマットを「理想的な成果物」として模倣してください。

### 適用例1：通常のクラス（XMLコメントのみ）

```csharp
/// <summary>
/// ユーザー認証およびセッション管理を提供します。
/// </summary>
public class UserService : IUserService
{
    /// <summary>
    /// セッションの有効期限（時間単位）。
    /// </summary>
    public int SessionTimeoutHours { get; set; }

    /// <summary>
    /// 指定された資格情報を使用してユーザーを認証します。
    /// </summary>
    /// <param name="username">ユーザー名。</param>
    /// <param name="password">プレーンテキストのパスワード。</param>
    /// <returns>認証が成功した場合はユーザー情報、それ以外の場合は <c>null</c>。</returns>
    /// <exception cref="ArgumentNullException"><paramref name="username"/> または <paramref name="password"/> が null の場合にスローされます。</exception>
    public User? Authenticate(string username, string password)
    {
        if (string.IsNullOrEmpty(username)) throw new ArgumentNullException(nameof(username));
        if (string.IsNullOrEmpty(password)) throw new ArgumentNullException(nameof(password));

        // 認証ロジック...
        return new User();
    }

    /// <inheritdoc />
    public void DeleteUser(int userId)
    {
        // インターフェースで定義済みのコメントを引き継ぐため、ここでは記述を省略
    }
}

```

### 適用例2：複雑な数理モデル・最適化を伴うメソッド（XML + ADRAnchor）

```csharp
/// <summary>
/// 与えられたデータポイントの集合に対して、最小二乗法を用いて単回帰分析（y = ax + b）を行います。
/// </summary>
/// <param name="x">独立変数の配列。要素数は2以上である必要があります。</param>
/// <param name="y">従属変数の配列。要素数は <paramref name="x"/> と一致している必要があります。</param>
/// <returns>回帰直線の傾き (a) と切片 (b) を含むタプル。</returns>
/// <exception cref="ArgumentException">配列の長さが異なる、または要素数が不足している場合にスローされます。</exception>
[ADRAnchor("M-01", nameof(FastWaveCompare))]
public (double Slope, double Intercept) CalculateLinearRegression(double[] x, double[] y)
{
    if (x.Length != y.Length || x.Length < 2) throw new ArgumentException("Invalid input data.");

    int n = x.Length;
    double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

    for (int i = 0; i < n; i++)
    {
        sumX += x[i];
        sumY += y[i];
        sumXY += x[i] * y[i];
        sumX2 += x[i] * x[i];
    }

    double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
    double intercept = (sumY - slope * sumX) / n;

    return (slope, intercept);
}

```
