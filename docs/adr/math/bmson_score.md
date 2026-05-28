---
adr-id: M-07
target-class: BmsScoreGenerator
status: open
---

# Bmsonスコア計算における数理モデル・設計判断

## Bmsonファイルのスコアジェネレータ
* 対象クラス: BmsScoreGenerator



**数学的証明 (Mathematical Proof)**

ノーツ数 $N$ における100%時のデフォルトTOTAL値 $T_{default}$ は、著名なBMS解析スクリプトである「black train近似式」に基づき以下で定義される。

$$
T_{default} = \max\left(260.0, \frac{7.605 \cdot N}{0.01 \cdot N + 6.5}\right)
$$

実際のBMS出力値 $T_{actual}$ は、bmsonの `total`（$T_{\%}$）を用いて、以下として算出する。

$$
T_{actual} = T_{default} \cdot \left( \frac{T_{\%}}{100.0} \right)
$$


**設計判断 (Why this algorithm?)**
bmsonの仕様では `total` 値は「IIDXのデフォルトゲージ回復量（100%）」に対するパーセンテージで指定されます。BMS（#TOTAL）には絶対値の回復量を実数で出力する必要があるため、ノーツ数からデフォルト回復量を逆算して適用しています。



