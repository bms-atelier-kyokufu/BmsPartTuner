## description: モダンなC#およびテスティングのベストプラクティスを基準に、テストコードのリファクタリング計画を立てます。

# テストコード・リファクタリング計画の策定

指定されたテストファイル（またはディレクトリ）を分析し、モダンな開発におけるテスト・リファクタリングのベストプラクティス（xUnit / C# 12以降を想定）に基づいて、リファクタリングの計画を立案してください。

## 分析・リファクタリングの基準（お手本となるパターン）

以下の基準に沿って、修正すべき箇所を特定し、リファクタリングのタスクリスト（計画）を作成してください。

### 1. 類似テストの `[Theory]` への統合 (Data-Driven Tests への移行)

似たような `Arrange -> Act -> Assert` の流れを持つ複数の `[Fact]` メソッドを、パラメータ化した1つの `[Theory]` メソッドと `[InlineData]` (または `[MemberData]`) に統合します。

- **Before (個別Factの乱立):**

    ```csharp
    [Fact]
    public void CalculateDiscount_RegularCustomer_ReturnsLowDiscount()
    {
        var calc = new DiscountCalculator();
        decimal discount = calc.GetDiscount(CustomerType.Regular, 10000);
        Assert.Equal(0.05m, discount);
    }

    [Fact]
    public void CalculateDiscount_VipCustomer_ReturnsHighDiscount()
    {
        var calc = new DiscountCalculator();
        decimal discount = calc.GetDiscount(CustomerType.Vip, 10000);
        Assert.Equal(0.15m, discount);
    }

    ```

- **After (Theory + InlineData):**

    ```csharp
    [Theory]
    [InlineData(CustomerType.Regular, 10000, 0.05, "一般顧客の標準割引")]
    [InlineData(CustomerType.Vip, 10000, 0.15, "VIP顧客の特別割引")]
    public void CalculateDiscount_Scenarios_ReturnsExpectedDiscount(CustomerType type, decimal amount, decimal expected, string scenario)
    {
        var calc = new DiscountCalculator();
        decimal discount = calc.GetDiscount(type, amount);
        Assert.Equal(expected, discount, $"シナリオ失敗: {scenario}");
    }

    ```

### 2. モック生成と I/O ロジックの共通ヘルパー集約 (DRY / Shared Test Helpers)

クラス内に重複して定義されているテストデータの生成（ダミーオブジェクト、フィクスチャ、スタブのセットアップ）を廃止し、共通のテストヘルパーやマザー（Test Data Mother）クラスへ処理を委ねます。

- **Before (クラスローカルのデータ生成ロジックの散在):**

    ```csharp
    private Customer CreateDummyCustomer(string name)
    {
        return new Customer
        {
            Id = Guid.NewGuid(),
            Name = name,
            Status = CustomerStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    ```

- **After (共通テストデータマザーの利用):**

    ```csharp
    // 共通のヘルパーまたはクラス外の定義に委ねる
    var customer = TestDataFactory.CreateActiveCustomer(name);

    ```

### 3. TheoryData の宣言的構築 (Tuple Collection Initializers)

`TheoryData` を生成する際、手続き的な `data.Add(...)` の呼び出しを廃止し、C# のコレクション初期化子 `{ (a, b), (c, d) }` による宣言的・表形式の記述に置き換えます。

- **Before (手続き的な Add):**

    ```csharp
    public static TheoryData<List<int>, int> GetTestData()
    {
        var data = new TheoryData<List<int>, int>();
        data.Add(new List<int> { 1, 2 }, 3);
        data.Add(new List<int> { 4, 5 }, 9);
        return data;
    }

    ```

- **After (宣言的なコレクション初期化子によるスマートな記述):**

    ```csharp
    public static TheoryData<List<int>, int> GetTestData() => new()
    {
        ( [1, 2], 3 ),
        ( [4, 5], 9 )
    };

    ```

### 4. アサーション・検証フローの共通化 (Assertion Helper / Wrapper Extraction)

テスト対象の呼び出し（Act）と検証（Assert）における共通パターン（特定の例外ハンドリング、定型的な状態チェック、リソースのラッパー等）を共通検証メソッド（`RunTest`等）へ抽出し、各テストケースのボイラープレートコードを削減します。

- **Before (毎回同じセットアップや検証構造を記述する):**

    ```csharp
    [Fact]
    public void Process_WithValidData_LogsSuccess()
    {
        using var context = TestContext.Create();
        var service = new Processor(context);
        var result = service.Process(validData);
        Assert.True(result.IsSuccess);
    }

    ```

- **After (検証ヘルパーメソッド経由でアサーションロジックを渡す):**

    ```csharp
    private void RunProcessorTest(TestData data, Action<ProcessResult> assertFunc)
    {
        using var context = TestContext.Create();
        var service = new Processor(context);
        var result = service.Process(data);
        assertFunc(result);
    }

    [Theory]
    [MemberData(nameof(GetProcessTestData))]
    public void Process_Scenarios(TestData data, bool expectedSuccess) =>
        RunProcessorTest(data, result => Assert.Equal(expectedSuccess, result.IsSuccess));

    ```

### 5. ファクトリメソッド経由のインスタンス化への移行 (Generic Factory / Fixture Pattern)

テストで使用するビルダーやモック、依存サービスをテストクラス内で直接 `new` して構築せず、テスト用コンテキストやフィクスチャオブジェクトが提供するファクトリメソッド `context.Create<T>()` を通して一元的に取得・管理するようにします。そのようにすることで、テストに`IDisposable`を実装していれば、`context`を`using`で囲むことで、リソースの解放漏れを防止できます。

- **Before (直接 new):**

    ```csharp
    var builder = new OrderBuilder(context);

    ```

- **After (コンテキストファクトリの利用):**

    ```csharp
    var builder = context.Create<OrderBuilder>();

    ```

### 6. Setup フェーズのモダン化 (LINQ と Collection Expression への置換)

ダミーのリストや配列を生成してオブジェクトに追加する際、手続き的な `for` / `foreach` ループを使用するのをやめ、C# 12 のコレクション式 `[...]` や LINQ を用いた宣言的な記述に置き換えます。

- **Before (forループによるデータ詰め込み):**

    ```csharp
    var items = new List<OrderItem>();
    for (int i = 1; i <= 5; i++)
    {
        items.Add(new OrderItem { Id = i, Price = 100 });
    }
    builder.AddItems(items);

    ```

- **After (LINQ とコレクション式によるワンライナー):**

    ```csharp
    builder.AddItems([.. Enumerable.Range(1, 5).Select(i => new OrderItem { Id = i, Price = 100 })]);

    ```

## 出力形式

以下のフォーマットでリファクタリング計画を出力してください：

### 1. 検出された問題点と改善案

（対象のファイル名・メソッド名とともに、どの基準をどう適用して改善するかをリストアップ）

### 2. リファクタリングタスクリスト

（Markdownのチェックボックス `[ ]` を用いて、実行すべき具体的な変更タスクを一覧化。すぐに作業に取り掛かれる粒度にすること）

**※エージェントへの指示**:
まずは対象ファイルを読み込み、上記の基準に従ってリファクタリング計画（プラン）をユーザーに提示してください。コードの修正はユーザーの承認を得てから開始してください。

