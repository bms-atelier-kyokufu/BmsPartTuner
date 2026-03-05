# BMS Part Tuner テスト設計書

## 1. 目的
本ドキュメントは、WPFアプリケーション「BmsAtelierKyokufu.BmsPartTuner」の品質保証戦略および詳細なテストケースを定義するものです。
最優先事項は**「データ非破壊性」**と**「処理の正確性」**です。

## 2. テスト戦略（Test Pyramid）

### Level 1: Unit Tests (Logic)
純粋なロジックを分離して検証し、高速なフィードバックを得ます。
- **対象**:
    - `RadixConvert`: 36進数/1296進数/62進数変換ロジック
    - `FastWaveCompare`: 音声波形比較ロジック（数学的正当性）
    - `WaveValidation`: 相関係数計算、SIMD演算
    - `DefinitionRangeManager`: 定義範囲管理
    - `AudioCacheManager`: 音声ファイル読み込み異常系、リソース管理

### Level 2: Integration Tests (Flow)
複数のコンポーネントが連携するシナリオを検証します。ファイルシステム操作を伴うテストはここに分類します（一時ファイル使用）。
- **対象**:
    - `BmsFileRewriter`: ファイル読み込み → 定義置換 → 書き出し
    - `BmsOptimizationService`: 一連の最適化フロー

### Level 3: UI/ViewModel Tests (Interaction)
MVVMパターンに基づき、ViewModelの状態遷移とコマンド実行を検証します。
- **対象**:
    - `MainViewModel`: 非同期実行、キャンセル処理、プログレス通知
    - `FileListFilterService`: フィルタリングロジック

## 3. 詳細テストケース

### A. BMS定義管理と書き換えロジック (`Core/Bms/`)

#### Unit Tests (`RadixConvert`, etc.)
- **36進数/1296進数変換の境界値**: `00`, `ZZ`, `01`, `10` などの変換が正しいか。
- **不正な入力**: 定義外の文字が含まれる場合の例外処理。

#### Integration Tests (`BmsFileRewriter`)
- **ファイル書き換えの安全性**:
    - 書き出しプロセスが完了するまで元のファイルを変更しないこと。
    - 書き出し中にエラーが発生した場合、不完全なファイルが生成されない（または元のファイルが残る）こと。
- **重複定義の統合**:
    - 異なる定義番号が同一ファイルを指す場合、正しく統合され、BMS内の記述も新しい番号に置換されるか。
- **BMS特有の複雑な挙動への対応**:
    - **大文字小文字の混在**: `#WAV01` / `#wav01`、ファイルパスの大文字小文字を同一視するか。
    - **構文ノイズ**: 定義行間の空行、スペース、非標準コメントがあってもパース可能か。
    - **文字エンコーディング**: Shift-JISで正しく読み書きできるか。
    - **未定義の参照**: Main Dataで使用されているが `#WAVxx` 定義がない番号の扱い（例外にならず、そのまま維持されるか）。
    - **仕様確定**: 未定義WAV定義は**「維持」**とする（`BmsFileRewriter`はこれらを削除せず、警告ログを出力する）。

### B. 音声比較エンジン (`Audio/`)

#### Unit Tests (`FastWaveCompare`, `WaveValidation`)
- **波形比較の精度**:
    - 同一ファイル（Correlation = 1.0）。
    - 逆相ファイル（Correlation = -1.0）。
    - 無音ファイル同士の比較。
- **エッジケース**:
    - 極端に短いファイル。
    - サンプリングレートやビット深度が異なるファイルの比較（即座に `false` または `0.0` を返すか）。
    - **無音の長さ違い**: 音声内容は同じだが前後の無音が異なる場合の判定（仕様に従う）。
    - **フォーマット不一致**: 比較不可として処理されるか。

#### Performance Tests (`Benchmarks/`)
- **処理速度検証**:
    - `FastWaveCompare` のSIMD演算がスカラー実装と比較して高速であることを確認。
    - アサーション例: `simdTime < scalarTime * 0.9`

### C. ViewModelとサービス連携

#### ViewModel Tests
- **非同期操作の状態管理**:
    - 実行中に `IsBusy` フラグが `true` になるか。
    - コマンドの `CanExecute` が適切に切り替わるか（二重実行防止）。
    - キャンセル要求（`CancellationToken`）が正しく伝播し、処理が中断されるか。
- **フィルタリング**:
    - リストのフィルタリング動作が正しいか。

### D. 異常系・環境ストレス

- **ファイルアクセス権限**:
    - 読み取り専用ファイルやロックされたファイルへの書き込み試行時に、適切な例外またはエラーメッセージが出るか。
- **パスの極端なケース**:
    - 長いパス、特殊文字を含むパスでの動作。
- **外部ファイル欠損**:
    - リンク切れのファイルがある状態での最適化実行。

## 4. テスト実装計画

1. **`FastWaveCompareTests` の実装**:
    - 既存の `WaveValidationTests` を補完し、`FastWaveCompare` クラス自体の振る舞い（キャッシュデータの比較ロジック）をテストする。
2. **`BmsFileRewriterTests` の実装**:
    - 一時ファイルを作成して実際に読み書きを行い、書き換え結果を検証する結合テストを作成する。
    - 特に「定義の置換漏れがないか」「定義の順序が正しいか」を確認する。

## 5. 技術的制約・使用ツール
- Framework: xUnit
- Mocking: Moq
- Assertions: xUnit Assertions

### テスト容易性への対応
- **Internalアクセスの許可**: `[InternalsVisibleTo("BmsAtelierKyokufu.BmsPartTuner.Tests")]` を `AssemblyInfo.cs` に設定し、リフレクションを使用せずに `internal` クラス・メソッドをテストします。
- **テスト用コンストラクタ**: `CachedSoundData` クラスに、ファイルI/Oを伴わずに波形データを注入できる `internal` コンストラクタを追加しています。