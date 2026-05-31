# BMS Part Tuner

[Read in English](README_EN.md)

[![BMS Part Tuner Logo](./BmsAtelierKyokufu.BmsPartTuner/Properties/Resources/BmpPartTunerLogo_dark.svg)](https://github.com/bms-atelier-kyokufu/BmsPartTuner/)

[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg?style=flat-square)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14.0-blue.svg?style=flat-square)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![WPF](https://img.shields.io/badge/WPF-Material%20Design%203-blue.svg?style=flat-square&logo=materialdesign)]()
[![Performance 8000x](https://img.shields.io/badge/Performance-8000x_Faster-brightgreen?style=flat-square&logo=speedtest)]()

## 概要

BMS Part Tunerは、BMSおよびBMSONフォーマットの譜面データにおいて、重複する音声パートを瞬時に統合・最適化するデスクトップアプリケーションです。
AIを用いた楽曲制作やステム分離後の譜面構築プロセスで発生しがちな「重くて管理しにくいデータ」を、純粋な計算機科学と数理論理学のアプローチで極限まで軽量化し、クリエイターが本来の創造的な作業に集中できる環境を提供します。

## 主な特徴

- **圧倒的な処理速度 (8000倍の高速化)**: 従来なら1時間かかるような $O(N^2)$ の総当たり比較処理を、アルゴリズムの最適化によってわずか300ミリ秒に短縮しました。
- **高度な最適化アルゴリズム**: SIMD（ベクトル化）、Flyweightパターンによるメモリ管理、空間の枝刈り（三角不等式）等を活用した高度な計算機科学技術を実装しています。
- **モダンなUI**: WPF（.NET 10）とMaterial Designを採用し、直感的で使いやすいインターフェースを実現しました。

## インストール方法

1. **必須要件の確認**:
    - OS: Windows 11
    - ランタイム: [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) 以降がインストールされている必要があります。
2. **ダウンロード**:
    - [Releasesページ](https://github.com/bms-atelier-kyokufu/BmsPartTuner/releases) より、最新のZIPファイルをダウンロードします。
3. **実行**:
    - ダウンロードしたZIPファイルを任意のフォルダに展開し、`BmsPartTuner.exe` を実行してください。インストール不要でそのまま動作します。

## 使い方

1. アプリケーションを起動します。
2. 対象となるBMS/BMSONファイル、またはフォルダを画面上にドラッグ＆ドロップします。
3. 必要に応じて「設定」から類似度判定のしきい値や、プレビュー用のプレイヤーパスを調整します。
4. 「最適化を実行」ボタンをクリックすると、瞬時に重複音声が統合され、クリーンな譜面データが生成されます。

## 推奨される制作ワークフロー

本ツールは、特に以下のステップを含む現代的な楽曲制作フローで最大の効果を発揮します。

1. 生成AIを用いた楽曲の作成
2. AIツールによるステム（パートごとの音源）の分離
3. BMS・BMSONエディタでの譜面構築
4. **本アプリケーション（BMS Part Tuner）による重複定義の統合と最適化**
5. 完成したパッケージの公開

## 詳細な技術ドキュメント（エンジニア向け）

BMS Part Tunerが数万個の音声ファイルを瞬時に処理できる最適化アルゴリズム（カスケード分類器、SIMD、Lock-free Anti-Set等）やアーキテクチャの詳細は、専用の技術ドキュメントサイトで公開しています。

**[技術ドキュメントを読む（GitHub Pages）](https://bms-atelier-kyokufu.github.io/BmsPartTuner/)**

## 動作環境

- **OS**: Windows 10 / 11
- **RAM**: 12GB以上を推奨（余剰領域3GB以上の確保を推奨）
- **CPU**: AVX2命令セットに対応したCPU（非対応環境でも動作しますが、パフォーマンスが低下します）
- **ランタイム**: .NET 10.0 以降

## ライセンス

本プロジェクトは、[BMS Part Tuner ソフトウェア使用許諾ライセンス](./LICENSE.md) のもとで公開されています。詳細は同梱の `LICENSE.md` ファイルをご確認ください。

## 開発・サポート

本ソフトウェアは [BMSアトリエ【極譜】](https://bms-atelier-kyokufu.blogspot.com/) によって開発・提供されています。

バグの報告や機能改善のご要望は、GitHubの [Issues](https://github.com/bms-atelier-kyokufu/BmsPartTuner/issues) にて受け付けています。お気軽にご投稿ください。

---

_私たちは、クリエイターを単純作業から解放し、楽曲のクオリティアップという本来の創造的な作業に集中してもらうことを目指しています。
「動作が軽く、管理しやすいデータ」を作ることは、プレイヤーへの配慮であると同時に、作品の完成度を高める重要な要素です。BMS Part Tunerは、その品質を支えるための信頼できるパートナーとなります。_
