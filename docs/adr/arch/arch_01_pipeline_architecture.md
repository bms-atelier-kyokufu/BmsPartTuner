---
adr-id: ARCH-01
title: Pipelineパターンの導入による最適化・変換フローの関心の分離
date: 2026-05-30
status: accepted
---

# ARCH-01: Pipelineパターンの導入による最適化・変換フローの関心の分離

## コンテキスト
BmsPartTuner のコア処理である「BMS最適化（`BmsOptimizationService`）」「定義削減（`DefinitionReuse`）」「bmson変換（`BmsonIntegrationFacade`）」などは、それぞれが多数のステップ（メタデータ解析、音声特徴抽出、波形比較、ファイル書き換え等）を内包する巨大なワークフローです。
従来の実装では、これらのステップが一つの巨大なメソッドやクラス内に手続き的に記述されており、以下の問題が生じていました。
- **God Class化**: 進行状況の報告、エラーハンドリング、キャンセレーション、ドメインロジックが一箇所に集中し、クラスが肥大化。
- **テスト容易性の低下**: 特定のステップだけを単体テストすることが困難。
- **拡張性の欠如**: 新しい変換ステップ（例: 新たなメタデータ抽出ロジック等）を途中に追加する際、既存の巨大なフローを直接改修する必要がある。

## 決定事項
全体の処理を独立した「Step（処理単位）」と「Context（状態の共有）」に分割し、それらを順次実行する **Pipelineパターン** を採用します。

1. **Contextの導入**: `OptimizationSimulationContext`, `DefinitionReductionContext`, `BmsonConversionContext` など、ワークフロー全体で共有すべき状態（入力データ、中間結果、進捗など）を保持する専用のクラスを定義します。
2. **Stepのインターフェース化**: `IAsyncOptimizationStep` などのインターフェースを定義し、各処理ステップを個別のクラスとして実装します。
3. **Pipelineエンジン**: `OptimizationSimulationPipeline` などのエンジンクラスが、Contextを保持しながら各Stepを順次実行し、横断的関心事（進捗通知、キャンセル処理、エラーハンドリング）を一元管理します。

## 影響範囲・結果
- **肯定的な結果**:
  - クラスの単一責任原則 (SRP) が向上し、各Stepのロジックが簡潔になりました。
  - 新規機能追加時は新しいStepクラスを作成してPipelineに登録するだけで済み、Open/Closed Principle (OCP) に準拠します。
  - 例外処理やキャンセルトークンの伝播がPipelineエンジン側に隠蔽され、ビジネスロジックの見通しが劇的に改善しました。
- **否定的な結果**:
  - Contextクラスが「何でも入る箱」になりやすく、状態のミュータビリティ管理に注意を払う必要があります。
  - クラスの数が急増するため、全体の処理の流れを把握するためにはPipeline構築部分（DIやファクトリ）を読む必要があります。

## ADRアンカー
- `OptimizationSimulationPipeline`
- `OptimizationSimulationContext`
- `IAsyncOptimizationStep`
- `DefinitionReductionPipeline`
- `DefinitionReductionContext`
- `BmsonConversionPipeline`
- `BmsonConversionContext`
