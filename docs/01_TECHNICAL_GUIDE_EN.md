# BMS Part Tuner: Technical Guide

<div style="display: flex; justify-content: center; margin:1em">
<img src="img/Document-Hierarchy_JP.svg" alt="Document Hierarchy" width="40%" >
</div>

BMS Part Tuner is a creator-focused utility that deeply integrates with BMS and bmson data structures, employing a linear algebra-based approach to optimize definitions.
This document details the mathematical foundations of our algorithms, the underlying architecture, and the specific techniques utilized for performance optimization.

## Background and Objectives (The "Why")

In modern BMS production workflows—especially those leveraging Generative AI for music creation and AI stem separation—the chart construction process in the bmson format often yields thousands of individual audio fragments.
This inevitably leads to waveform-level redundant definitions, causing severe project bloat and runtime memory pressure for end-users.

This project was engineered to solve this exact problem, shifting the burden from "manual creator effort" to "logical identity evaluation." Our primary goal is to achieve the absolute minimal definition structure without compromising the perceived auditory quality.

## Architecture and Design Philosophy

The project is built on .NET 10 and WPF, prioritizing high maintainability and strict type safety.

### Separation of Concerns (SoC)

Respecting WPF's core design philosophy, we have strictly applied the MVVM pattern. The UI logic and the optimization engine are completely decoupled. This ensures highly flexible testing through Dependency Injection (via `Microsoft.Extensions.DependencyInjection`).

- **Infrastructure** : Utilizes Behavior-based functional extensions to implement complex UI interactions while maintaining the declarative nature of XAML.
- **Services** : Abstracts audio playback, file I/O, and optimization logic, ensuring components are easily interchangeable and testable.
- **Design Tokens** : Centralizes resource definitions (colors, properties) to guarantee UI consistency across the application.

## Core Algorithm

For detecting audio identity, we utilize a correlation analysis algorithm explicitly optimized for acoustic signals.

### Quantitative Evaluation via Pearson Correlation Coefficient

To evaluate similarity, we quantify the resemblance of the waveform's "shape" on a scale from $0.0$ to $1.0$ based on the following definition:

$$r = \frac{\sum_{i=1}^{n} (x_i - \bar{x})(y_i - \bar{y})}{\sqrt{\sum_{i=1}^{n} (x_i - \bar{x})^2 \sum_{i=1}^{n} (y_i - \bar{y})^2}}$$

- $x, y$ : The two audio waveform data arrays being compared.
- $x_i, y_i$ : The amplitude value of the sample at a specific index $i$ (typically ranging from $-1.0$ to $+1.0$).

Because audio waveforms naturally exhibit a mean amplitude ($\bar{x}, \bar{y}$) extremely close to $0$, we mathematically simplify this equation to the equivalent of Cosine Similarity in our implementation. This transformation drastically reduces the computational cost:

$$r \approx \frac{\sum_{i=1}^{n} x_i y_i}{\sqrt{\sum_{i=1}^{n} x_i^2} \sqrt{\sum_{i=1}^{n} y_i^2}}$$

### Computational Performance Optimization

To process tens of thousands of file combinations within seconds, we implemented the following engineering techniques.

#### Energy Pre-calculation

We pre-calculate the energy term $E_u$ (the denominator) during the initial indexing phase. By purifying the inner comparison loop to only compute the numerator, we eliminate the execution of expensive square root and division operations almost entirely.

First, for any given audio waveform $u$, its autocorrelation component (waveform energy) $E_u$ is defined as:

$$E_u = \sqrt{\sum_{i=1}^{n} u_i^2}$$

The waveform energy $E_u$ for each file is cached upfront. During the actual comparison, the engine only executes the following calculation using the Dot Product (inner product):

$$r = \frac{\sum_{i=1}^{n} x_i y_i}{E_x \cdot E_y}$$

Here, the numerator $\sum x_i y_i$ represents the sum of the products of corresponding samples on the time axis, which is mathematically equivalent to the dot product in vector operations.

#### Amplitude Invariance and Implicit Normalization

A critical feature of this algorithm is its independence from amplitude scaling (volume differences). Mathematically, the correlation coefficient $r$ is invariant to the scalar multiplication of vectors $x, y$. **Benefit:** This completely eliminates the need for users or the system to perform a "Normalization" preprocessing step to equalize audio volume. Even if minute volume differences are introduced during stem separation or DAW exporting, waveforms with identical "shapes" are correctly identified as identical. The algorithm inherently performs implicit volume equalization during its evaluation.

#### Early Pruning

If the accumulated dot product makes it mathematically impossible to reach the required theoretical threshold, the loop for the remaining samples is immediately aborted. This aggressive pruning eliminates wasted CPU cycles on doomed comparisons.

## Project Documentation

For detailed specifications and design guidelines, please refer to the following documents:

- [00_ARCHITECTURE_EN](./00_ARCHITECTURE_EN.md) (Planned)
  Overview of the entire system's component structure and class design.
- [02_OPTIMIZATION_GUIDE_EN.md](./02_OPTIMIZATION_GUIDE_EN.md) (Planned)
  Scope of optimization logic and specific processing flows.
- [COMMIT_MESSAGE_GUIDE_EN.md](./COMMIT_MESSAGE_GUIDE_EN.md) (Planned)
  Commit conventions for participating in development.
- [04_TEST_STRATEGY_EN.md](./04_TEST_STRATEGY_EN.md)
  Design guidelines for unit and mutation testing.

BMS Part Tuner is an engineering endeavor aimed at empowering creator creativity through technology. We hope these technical optimizations contribute to the birth of highly refined, sophisticated BMS works.