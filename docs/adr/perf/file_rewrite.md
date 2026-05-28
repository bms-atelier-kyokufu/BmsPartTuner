---
adr-id: OPT-06
target-class: BmsFileRewriter
status: open
---

# BMSファイルの置換アルゴリズムと未定義参照の非破壊処理

## BMSファイル解析および書き換えモジュール
* 対象クラス: BmsFileRewriter, BmsDefinitionManager




**設計判断 (Why this algorithm?)**
- **3段階マップ構築による置換と整列 (`BmsFileRewriter`)**:
  削減されたWAV定義を整理するため、不要になった定義を削除し、残った定義をファイル名順にソートした上で新しいID（01, 02, ...）を振り直す処理を行います。ファイル名順にソートすることで、類似する音声（kick_01, kick_02...）がBMS定義上で連続して配置され、視認性が向上します。
- **未定義参照の非破壊処理 (`BmsFileRewriter`)**:
  元のBMSファイル内で `#WAV` 定義が散在している場合でも、再出力時はファイルの先頭（最初の `#WAV` 宣言位置）に一括でまとめます。
- **BMS定義マネージャーの責務 (`BmsDefinitionManager`)**:
  BMSファイルから解析した#WAV定義を管理し、WPFなどのUIにデータバインディングを提供するため、内部で `ObservableCollection<BmsAudioFile>` を保持します。また、単一のBMSファイルを対象とするため、楽器検出サービス（`InstrumentNameDetectionService`）のライフサイクルもこのクラス内で完結させます。
- **基数の自動判定 (`BmsDefinitionManager`)**:
  BMSフォーマットは標準で36進数（0-9,A-Z）ですが、拡張仕様として62進数（0-9,A-Z,a-z）も存在します。定義値に小文字が含まれるかどうかを判定することで、適切な基数（Radix）を自動決定しています。

**非採用としたアプローチ (Rejected Alternatives / Anti-patterns)**
- **一段階での置換処理の廃止 (`BmsFileRewriter`)**:
  BMSファイルの譜面データを走査しながら逐次的に新しいIDを決定すると、順序の制御が困難になります。そのため、以下の3段階のマップを構築する手法を採用しました。
  1. reductionMap: 元のID → 削減後のID（重複排除）
  2. reducedToNewMap: 削減後のID → 新しいID（整列後の連番）
  3. finalMap: 元のID → 新しいID（最終的な置換マップ）
  これにより、重複の排除と再整列をそれぞれ独立してO(N)で処理できます。
- **未定義参照の強制削除の廃止 (`BmsFileRewriter`)**:
  譜面データ内で参照されているが定義リストに存在しないID（例：元のBMSファイル自体のバグ等）を空白や `00` に置換してしまうと、ユーザーの既存資産を破壊するリスクがあります。「データ非破壊の原則」に従い、マップに存在しないIDはそのまま維持します。
- **ObservableCollectionへの逐次追加の廃止 (`BmsDefinitionManager`)**:
  `ObservableCollection<T>` の `Add` メソッドをループ内で呼び出すと、都度 `CollectionChanged` イベントが発火し、UIのレンダリングをブロックしてパフォーマンスが著しく低下します。これを避けるため、一時的な `List<T>` で全要素を構築し、推定処理も全て完了した後に一括で追加・通知を行うアプローチを採用しています。



