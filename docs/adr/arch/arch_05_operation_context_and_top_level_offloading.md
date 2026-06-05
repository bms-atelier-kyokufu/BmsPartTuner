---
adr-id: ARCH-05
title: OperationContext と Top-Level Offloading
date: 2026-06-06
status: accepted
---

# Architecture Decision Record: OperationContext and Top-Level Offloading

## Context & Problem

BmsPartTuner における非同期処理とUI応答性に関して、以下の2つの深刻なアーキテクチャ上の問題が発生していました。

1. **UIスレッドのフラッディング (UI Thread Flooding)**:
    - `BmsOptimizationService` の `Parallel.ForEach` 内から `Progress<int>.Report` が毎秒数百回も呼ばれており、これが全てUIスレッドのDispatcherにマーシャリングされていました。
    - 結果としてUIのメッセージポンプが詰まり（フラッディング）、キャンセルボタンを押しても数秒〜十数秒反応しない、という致命的なUIのフリーズ状態を引き起こしていました。
2. **スレッド責務の混在と同期ブロック**:
    - UI ViewModel, UseCase, Serviceの各レイヤーで場当たり的に `await Task.Run` が散在し、逆に重い処理（例: `AudioCacheManager.PreloadAudioData`）が `Task.Run` に包まれずUIスレッドで同期的に実行されてしまうバグが起きていました（"Sync-over-Async" アンチパターンの一種）。
3. **パラメータの肥大化 (Parameter Bloat)**:
    - 非同期処理を正しくキャンセル・進捗報告するため、`CancellationToken` と `IProgress<T>` をServiceの末端メソッドやユーティリティまで全て引き回す必要があり、メソッドシグネチャの美しさとドメインモデリングを損なっていました。

## Decision

これらの問題に対処するため、以下の3つのアーキテクチャデザインパターンを採用します。

### 1. Top-Level Offloading (最上位でのバックグラウンド移行)

ViewModel層がUseCaseを呼び出す際、必ず **最上位で一括して `await Task.Run(...)` でラップ** します。
UseCase層およびService層以降の内部では原則として `Task.Run` を記述しません。

- **利点**: Service層以下の処理は「常にスレッドプールで実行されている」ことがシステム的に保証され、誤ってUIスレッドをブロックする事故が構造的に排除されます。

### 2. Parameter Object Pattern: `IOperationContext` の導入

`CancellationToken` と `IProgress<T>`、さらに必要に応じた共通コンテキスト（ロギング等）をまとめたパラメータオブジェクトとして `IOperationContext` インターフェース（および実装の `OperationContext`）を導入します。
各メソッドは個別のトークンやプログレスではなく、`IOperationContext` を引数として受け取ります。

- **利点**: C# の TAP (Task-based Asynchronous Pattern) のセオリーに従いつつも、引数をオブジェクト化することでシグネチャをすっきりと保ちます。また、将来的なコンテキスト情報の追加にも容易に対応できます。

### 3. Progress Throttling (進捗報告のスロットリング)

UIスレッドのフラッディングを防ぐため、`IProgress<T>` のラッパーとして `ThrottledProgress<T>` を導入します。
内部でタイマーと状態を管理し、「前回から一定時間（例: 50ms）経過したか、100%に達した時のみ」実際の `Progress<T>.Report` を発火させます。

- **利点**: 末端の `Parallel.ForEach` 側で進捗報告の頻度を気にする必要がなくなり、純粋に「1回終わったからReportする」という素朴なコードを保ちながら、UIの応答性を劇的に向上させます。

## Consequence

- ViewModelからUseCaseを呼び出すコードは、`OperationContext.Create` と `Task.Run` でラップされる定型的な構造になります。
- `BmsOptimizationService` などの各メソッドのシグネチャは `(..., IOperationContext context)` に統一されます。
- キャンセルボタンは常に即座に反応し、UIスレッドが重くなる現象は解消されます。
