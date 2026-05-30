# BMS Part Tuner: 技術ドキュメント

**BMS Part Tuner** の開発者向け・技術ドキュメントサイトへようこそ。
このサイトでは、アプリケーションを支えるアーキテクチャ、数理モデル、そして極限の最適化技術について深く掘り下げています。

## "8000倍"の最適化カスケード

BMS Part Tunerは、従来の $O(N^2)$ の総当たり比較を排除し、純粋な計算機科学と数理論理学に基づくカスケードアルゴリズムによって極限のパフォーマンスを実現しています。

```mermaid
flowchart TD
    %% スタイル定義（意味論的カラーリング）
    classDef startNode fill:#f3f4f6,stroke:#4b5563,stroke-width:2px,color:#1f2937;
    classDef processNode fill:#dbf4ff,stroke:#0284c7,stroke-width:2px,color:#0c4a6e;
    classDef filterNode fill:#fef08a,stroke:#ca8a04,stroke-width:2px,color:#713f12;
    classDef successNode fill:#bbf7d0,stroke:#16a34a,stroke-width:2px,color:#14532d;
    classDef rejectNode fill:#fecaca,stroke:#dc2626,stroke-width:2px,color:#7f1d1d;

    %% 起点
    Start(["すべての音声ファイルペア"]):::startNode

    %% メインパイプライン（成功ルート：ブルー）
    Start -->|"第1ステージ：O(1) 既知の不一致判定"| S1["Lock-free Anti-Set"]:::processNode
    S1 ==>|"第2ステージ：O(N) 局所性鋭敏型ハッシュ"| S2["SimHash256"]:::processNode
    S2 ==>|"第3ステージ：16次元特徴量 ユークリッド距離"| S3["FFT 16D 特徴量"]:::processNode
    S3 ==>|"最終ステージ：極限SIMDドット積"| S4["Z-score正規化波形比較 (Pearson)"]:::processNode
    S4 ===> Success(["100%正確なマージ"]):::successNode

    %% 足切りルート（除外判定：イエロー）
    S1 -.->|"不一致として記録済"| R1["比較をスキップ (O(1))"]:::filterNode
    S2 -.->|"ハミング距離 ＞ 64"| R2["超高速足切り (数クロック)"]:::filterNode
    S3 -.->|"特徴量距離大"| R3["高速足切り (1パス)"]:::filterNode

    %% 足切りの収束先（破棄ルート：レッド）
    R1 --> Reject(["不一致として除外"]):::rejectNode
    R2 --> Reject
    R3 --> Reject
```

## ナビゲーション

左側のサイドバー、または以下のリンクから各種技術ドキュメントをご覧ください。

* **[アーキテクチャ概要](00_ARCHITECTURE_JP.md)** - システム全体の設計思想
* **[テクニカルガイド](01_TECHNICAL_GUIDE_JP.md)** - 最適化アルゴリズムの詳細と数理モデル
* **[パフォーマンス最適化](02_OPTIMIZATION_GUIDE_JP.md)** - 「8000倍」の高速化をもたらした実装手法
* **[bmson変換システム](05_BMSON_CONVERSION_JP.md)** - メモリ最適化とキャッシュによる高速ダウンコンバート
* **[テスト設計](03_TEST_DESIGN_JP.md)** / **[コミット規約](COMMIT_MESSAGE_GUIDE_JP.md)** - 開発・品質保証プロセス

_(Note: ヘッダーの言語スイッチャーから英語版（English）のドキュメントに切り替えることも可能です)_

