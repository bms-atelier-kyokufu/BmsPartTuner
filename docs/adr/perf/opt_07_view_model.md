---
adr-id: OPT-07
target-class: OptimizationViewModel
status: accepted
---

# UI最適化：遅延ローダーパターンと意図的GC

## 最適化画面 ViewModel

- 対象クラス: OptimizationViewModel

**設計判断 (Why this algorithm?)**

- **遅延ローダーパターンの採用**:
  非同期処理が即座（数十ミリ秒以内）に完了する場合に、UI上にローディング表示が一瞬だけ点滅して描画されると、ユーザー体験が著しく損なわれます（チラつき問題）。IsBusy と LoaderVisibility を直接バインディングすると、高速な処理時に必ずチラつくため、一定時間（AppConstants.UI.LoaderDelayMs）待機した後に、まだキャンセルされていなければ初めて ShowLoader を true にする「遅延ローダーパターン」を採用しています。
- **意図的な `GC.Collect()` の実行**:
  大量の音声データ（最大数百MB）をオンメモリで解析・比較するため、ジェネレーション2（Large Object Heap等）に大量のオブジェクトが短期間にアロケーションされます。WPFアプリケーションでは、UIスレッドがアイドル状態であってもバックグラウンドのGCがLOHの回収を遅延させ、結果として次のシミュレーション実行時に `OutOfMemoryException` が発生するリスクがあります。これを防ぐため、処理完了という明確なライフサイクルの区切りで意図的に `GC.Collect()` を実行し、メモリの肥大化を抑止しています。
