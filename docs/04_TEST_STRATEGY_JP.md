# BMS Part Tuner テスト戦略書

## 概要

このドキュメントは、BMS Part Tunerプロジェクトのテスト戦略を定義します。  
**目標**: 1人月で保守可能な、実用的かつ堅牢なテストスイートを維持する。

## テスト方針

### コアロジック重視アプローチ

本プロジェクトのテストは、**ビジネスロジックの正確性**に焦点を当てています。  
UI補助機能や単純なプロパティGet/Setのテストは意図的に削減されています。

### テストピラミッド

```
        /\
       /  \      統合テスト (Scenarios)
      /____\     
     /      \    ドメインロジック (Core, Services)
    /________\   
   /          \  ユーティリティ (Helpers, Converters)
  /____________\ 
```

##  必須維持テスト (Whitelist)

以下のテストは**絶対に削除禁止**です。ロジック変更時以外は触れないでください。

### 1. 最適化エンジンの核心

| テストファイル | 目的 | 重要度 |
|--------------|------|--------|
| `Core/Optimization/SimulationEngineTests.cs` | しきい値と削減数の計算ロジック検証 | ⭐⭐⭐ |
| `Core/Helpers/AudioFileGroupingStrategyTests.cs` | 音声ファイルのグルーピング精度（データ破壊防止） | ⭐⭐⭐ |

### 2. BMSデータ操作・計算

| テストファイル | 目的 | 重要度 |
|--------------|------|--------|
| `Core/Bms/BmsFileRewriterTests.cs` | 定義番号置換の正規表現ロジック（データ破壊防止） | ⭐⭐⭐ |
| `Core/Bms/BmsFileRewriterTests_Atomic.cs` | ファイル書き込みの原子性保証 | ⭐⭐⭐ |
| `Core/Bms/DefinitionRangeManagerTests.cs` | 定義番号範囲計算の境界値テスト | ⭐⭐ |
| `Core/Bms/DefinitionStatisticsTests.cs` | 統計情報の計算精度 | ⭐⭐ |
| `Core/Helpers/RadixConvertTests.cs` | 36進数/62進数変換の正確性 | ⭐⭐ |

### 3. 入力バリデーション

| テストファイル | 目的 | 重要度 |
|--------------|------|--------|
| `Core/Validation/BmsValidatorsTests.cs` | ユーザー入力の境界値・異常値テスト | ⭐⭐ |

### 4. 音声処理

| テストファイル | 目的 | 重要度 |
|--------------|------|--------|
| `Audio/ParallelAudioComparisonEngineTests.cs` | 並列音声比較エンジンの正確性 | ⭐⭐⭐ |
| `Audio/FastWaveCompareTests.cs` | 音声一致判定ロジックの精度 | ⭐⭐ |
| `Audio/WaveValidationTests.cs` | 相関係数計算の数学的正確性 | ⭐⭐ |
| `Audio/AudioCacheManagerTests.cs` | キャッシュ管理の一貫性 | ⭐⭐ |

### 5. サービス層

| テストファイル | 目的 | 重要度 |
|--------------|------|--------|
| `Services/BmsOptimizationServiceTests.cs` | 最適化処理の統合テスト | ⭐⭐⭐ |
| `Services/BmsOptimizationServiceTests_Deletion.cs` | ファイル物理削除の安全性 | ⭐⭐⭐ |
| `Services/AudioPreviewServiceTests.cs` | 音声プレビュー機能の動作確認 | ⭐ |
| `Services/ResultCardServiceTests.cs` | 結果表示ロジックの一貫性 | ⭐ |

### 6. ViewModel（状態遷移のみ）

| テストファイル | 目的 | 重要度 |
|--------------|------|--------|
| `ViewModels/OptimizationViewModelTests.cs` | 最適化実行時の状態遷移 | ⭐⭐ |
| `ViewModels/OptimizationViewModelTests_SlideState.cs` | スライド確認UIの状態管理 | ⭐⭐ |

### 7. ユーティリティ

| テストファイル | 目的 | 重要度 |
|--------------|------|--------|
| `Converters/CorrelationCoefficientConverterTests.cs` | 相関係数表示変換の精度 | ⭐ |
| `Core/AppConstantsTests.cs` | 定数値の整合性 | ⭐ |

## 統合テスト

| テストファイル | 目的 | 重要度 |
|--------------|------|--------|
| `Scenarios/OptimizationScenarioTests.cs` | エンドツーエンドのシミュレーション（インメモリ完結） | ⭐⭐⭐ |

**重要**: このテストはファイルI/Oを使用せず、メモリ内で音声データを生成して高速実行します。

## ミューテーションテスト

`RoslynMutation/` 以下のテストは**現状維持**とします。  
コードの堅牢性を定量的に測定するため、実行環境のスペックが許す限り維持します。

## メトリクス目標

| 指標 | 目標値 | 現状値 |
|-----|-------|-------|
| テストファイル数 | 20〜30個 | 約25個 |
| 実行時間（CI） | < 5分 | TBD |
| コアロジックカバレッジ | > 90% | TBD |
| UIレイヤーカバレッジ | > 30% | TBD |

## メンテナンス方針

### テスト追加基準

新規テストを追加する際は、以下の基準を満たす必要があります：

1. **データ破壊リスクが高いロジック**: ファイル操作、定義番号置換など
2. **複雑な計算ロジック**: 相関係数、統計処理など
3. **境界値・異常値処理**: バリデーション、範囲チェックなど
4. **状態遷移が複雑なViewModel**: ビジネスロジックを含むもの

### テスト削除基準

以下のパターンに該当するテストは削除を検討してください：

1. **ライブラリ機能のテスト**: CommunityToolkit.Mvvm、標準ライブラリの動作確認
2. **単純なプロパティGet/Set**: ロジックを含まないもの
3. **PropertyChangedイベントのみの確認**: MVVMフレームワークの責務
4. **UI補助機能**: フィルター、ソート、表示変換など

## 開発フロー

1. **機能追加時**: コアロジックに影響する場合のみテスト追加
2. **リファクタリング時**: 既存テストがパスすることを確認
3. **バグ修正時**: 再発防止のテストを追加（判断基準に従う）
4. **定期レビュー**: 四半期ごとにテスト構成を見直し
