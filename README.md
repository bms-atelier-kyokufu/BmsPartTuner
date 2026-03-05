# BMS Part Tuner

[![BMS Part Tuner Logo](https://raw.githubusercontent.com/bms-atelier-kyokufu/BmsPartTuner/refs/heads/main/BmsAtelierKyokufu.BmsPartTuner/Properties/Resources/BmpPartTunerLogo_dark.svg)](https://github.com/bms-atelier-kyokufu/BmsPartTuner/)


[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg?style=flat-square)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-14.0-blue.svg?style=flat-square)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![WPF](https://img.shields.io/badge/WPF-Material%20Design%203-blue.svg?style=flat-square&logo=materialdesign)]()
[![Performance 8000x](https://img.shields.io/badge/Performance-8000x_Faster-brightgreen?style=flat-square&logo=speedtest)]()

# For Recruiters & Engineers (Technical Deep Dive)

If you are a recruiter or an engineer interested in the technical aspects of this project, please refer to the following English documentation. This project demonstrates high-performance optimization in **.NET 10** and **WPF**.

## Performance Highlights

- 8000x Speedup: Reduced audio processing time from 1 hour to 300 milliseconds.
- Tech Stack: .NET 10, SIMD (Vectorization), Memory-Mapped Files, MVVM, and DI.

##  Key Documents

- [Technical Portfolio Summary](docs/PORTFOLIO_SUMMARY_EN.md): A high-level overview of the architecture, optimization, and QA strategy.
- [Optimization Engineering Report](docs/OPTIMIZATION_GUIDE_EN.md): A deep dive into how I achieved the 800x performance boost using low-level .NET techniques.

---

## **非破壊的な譜面更新**

譜面データそのものを破壊することなく、内部の音声定義と参照のみを書き換えます。既存の配置がずれる心配はなく、安全に最適化を実行できます。

## **直感的なユーザーインターフェース**

最新のMaterial Design 3に基づいたモダンなインターフェースを採用しました。複雑な設定を覚える必要はなく、ドラッグアンドドロップからスムーズに最適化を開始できるよう設計されています。

## **推奨される制作ワークフロー**

本ツールは、特に以下のステップを含む現代的な楽曲制作フローで最大の効果を発揮します。

1. 生成AIによる楽曲の作成  
2. AIツールを用いたステム（パートごとの音源）の分離  
3. BMSONエディタでの譜面構築
4. [BMX2WAV](https://childs.squares.net/program/bmx2wav/index.html)による音切り  
5. 本アプリケーションBMS Part Tunerによる重複定義の統合と最適化  
6. 完成したパッケージの公開

## **BMSアトリエ【極譜】からの制作メッセージ**

私たちは、クリエイターを単純作業から解放し、楽曲のクオリティアップという本来の創造的な作業に集中してもらうことを目指しています。  
「動作が軽く、管理しやすいデータ」を作ることは、プレイヤーへの配慮であると同時に、作品の完成度を高める重要な要素です。BMS Part Tunerは、その品質を支えるための信頼できるパートナーとなります。

## **動作環境**

* OS：Windows 10 / 11  
* ランタイム：.NET 10.0 以降

## **ライセンス**

本プロジェクトは[BMS Part Tuner ソフトウェア使用許諾ライセンス](https://github.com/bms-atelier-kyokufu/BmsPartTuner/blob/develop/LICENSE.md) のもとで公開されています。詳細は同梱のLICENSEファイルをご確認ください。

## **開発・サポート**
本ソフトウェアは[BMSアトリエ【極譜】](https://bms-atelier-kyokufu.blogspot.com/) によって開発・提供されています。
バグ報告や機能要望は、GitHubの[Issues](https://github.com/bms-atelier-kyokufu/BmsPartTuner/issues)で受け付けています。

開発者の方は[docs/Technical Guide.md](https://github.com/bms-atelier-kyokufu/BmsPartTuner/blob/main/docs/01_TECHNICAL_GUIDE.md)をご覧ください。内部の仕組みを詳しく解説しています。
