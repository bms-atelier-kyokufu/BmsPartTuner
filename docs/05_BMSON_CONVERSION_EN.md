# Bmson to BMS Down-conversion System

In addition to its BMS optimization capabilities, BMS Part Tuner features a **high-performance down-conversion system** from the bmson format to the legacy BMS format.
This document explains the architecture and optimization techniques used to process thousands of audio slices quickly and safely with limited memory and CPU resources.

## 1. Pipeline Architecture (Workflow Separation)

Converting bmson to BMS is a massive process that goes far beyond simple JSON deserialization. It involves metadata conversion, audio feature extraction, audio waveform slicing, and file output.
Implementing this as a monolithic process would severely degrade maintainability and testability.

To solve this, we adopted a **Context-based Pipeline Architecture**:

- **`BmsonConversionContext`**: A dedicated context class that holds the state to be shared across the entire workflow (input data, intermediate products, progress status, etc.).
- **`BmsonConversionPipeline`**: A pipeline that sequentially executes individual processing steps, passing the context along. Each step implements an interface like `IConversionStep` and is independently testable, adhering to the Single Responsibility Principle (SRP).

This encapsulates the internal state of the complex down-conversion process and enhances thread safety during parallel execution.

## 2. Memory Optimization via Flyweight Pattern (Avoiding OOM)

A defining characteristic of the bmson format is that it plays back "slices" of a single long base WAV file by specifying a start position (`offset`) and length (`duration`).
A single bmson file can contain hundreds to thousands of such slice definitions.

If we allocated a separate `float[]` (waveform data) on the heap for each of these audio slices during down-conversion, we would duplicate the same base waveform in memory countless times, easily triggering an **OutOfMemoryException (OOM)**.

To address this, our system employs an approach based on the **Flyweight Pattern** (`PointerSoundData`).

- **Shared References**: The sliced audio objects do not hold the actual waveform arrays. Instead, they hold a reference to the base audio data, along with their own start index and length.
- **Utilizing `ReadOnlySpan<float>`**: When the waveform data is actually needed, it is sliced from the base waveform array and returned as `.AsSpan(start, length)`.
  This reduces data copying (allocation) to zero. No matter how many slices are created, memory consumption is strictly limited to that of the single base waveform, achieving extreme optimization.

## 3. 2-Level Cache Strategy for Audio Slicing

During the phase where the bmson note array is processed to output physical sliced WAV files, another performance bottleneck emerged: identical slice requests (same `fileName`, `offsetSec`, and `durationSec`) occurred repeatedly.

Previously, silence trimming scans were performed for every single request. When tens of thousands of requests pile up, this seemingly small operation becomes a bottleneck causing hundreds of milliseconds to seconds of delay.
To avoid this, we implemented the following **2-Level Cache mechanism**:

1. **L1 Request Cache (O(1) Lookup)**
   Using the requested parameter string (e.g., `fileName|startByte|lengthBytes`) as a key, the system instantly returns the previously generated file name. If there's a hit, expensive memory access and silence trimming calculations are **completely skipped**.

2. **L2 Slice Cache (Preventing Duplicate File Generation)**
   This cache operates at the final stage of writing to physical files, after passing the silence trimming calculation. To prevent file I/O conflicts during parallel processing, it uses `Lazy<string>` to generate and cache files asynchronously and thread-safely.

While pre-aggregating with LINQ `GroupBy` was considered, it was rejected to avoid Garbage Collection (GC) pauses caused by intermediate collection allocations. The current "Request-driven + L1 Cache" approach was chosen for being allocation-free and highly performant.

## 4. Score (TOTAL) Conversion via Mathematical Model

In bmson, the `total` value (gauge recovery amount) is specified as a percentage relative to the "IIDX default gauge recovery amount (100%)".
However, in standard BMS format (`#TOTAL`), the recovery amount must be specified as an absolute "real number". Failing to convert this correctly ruins the gameplay experience.

Our system solves this by introducing the **"black train approximation formula"**—a mathematical model used in famous BMS analysis scripts—to reverse-calculate the default recovery amount from the number of notes.

The default TOTAL value $T_{default}$ for a given number of notes $N$ is calculated as follows:

$$
T_{default} = \max\left(260.0, \frac{7.605 \cdot N}{0.01 \cdot N + 6.5}\right)
$$

The actual BMS output value $T_{actual}$ is then calculated using the bmson's `total` value ($T_{\%}$):

$$
T_{actual} = T_{default} \cdot \left( \frac{T_{\%}}{100.0} \right)
$$

Thanks to this mathematical model, the gauge recovery percentage intended by the bmson creator is reproduced with extreme accuracy in the converted BMS file.

