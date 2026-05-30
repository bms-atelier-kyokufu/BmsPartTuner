# BMS Part Tuner: Technical Documentation

Welcome to the technical documentation site for **BMS Part Tuner**. 
This site contains deep dives into the architecture, mathematical models, and extreme optimizations that power the application.

## The "8000x" Optimization Cascade

BMS Part Tuner achieves its extreme performance by eliminating the standard $O(N^2)$ brute-force comparisons and replacing them with a highly optimized cascade of algorithms powered by pure computer science and mathematical logic.

```mermaid
flowchart TD
    %% Semantic Styling
    classDef startNode fill:#f3f4f6,stroke:#4b5563,stroke-width:2px,color:#1f2937;
    classDef processNode fill:#dbf4ff,stroke:#0284c7,stroke-width:2px,color:#0c4a6e;
    classDef filterNode fill:#fef08a,stroke:#ca8a04,stroke-width:2px,color:#713f12;
    classDef successNode fill:#bbf7d0,stroke:#16a34a,stroke-width:2px,color:#14532d;
    classDef rejectNode fill:#fecaca,stroke:#dc2626,stroke-width:2px,color:#7f1d1d;

    %% Entry Point
    Start(["All Audio File Pairs"]):::startNode

    %% Main Pipeline (Success Route: Blue)
    Start -->|"Stage 1: O(1) Known Mismatch"| S1["Lock-free Anti-Set"]:::processNode
    S1 ==>|"Stage 2: O(N) LSH"| S2["SimHash256"]:::processNode
    S2 ==>|"Stage 3: 16D Euclidean Distance"| S3["FFT 16D Features"]:::processNode
    S3 ==>|"Final Stage: Extreme SIMD Dot Product"| S4["Z-score Normalized Waveform Comparison (Pearson)"]:::processNode
    S4 ===> Success(["100% Accurate Merge"]):::successNode

    %% Early Rejection Route (Filter: Yellow)
    S1 -.->|"Recorded as Mismatch"| R1["Skip Comparison O(1)"]:::filterNode
    S2 -.->|"Hamming Dist > 64"| R2["Ultra-fast Rejection (Few Clocks)"]:::filterNode
    S3 -.->|"Large Feature Distance"| R3["Fast Rejection (1 Pass)"]:::filterNode

    %% Rejection Destination (Red)
    R1 --> Reject(["Excluded as Mismatch"]):::rejectNode
    R2 --> Reject
    R3 --> Reject
```

## Navigation

Please explore the various technical documents using the left sidebar or the links below:

* **[Architecture](00_ARCHITECTURE_EN.md)** - Overall system design and patterns
* **[Technical Guide](01_TECHNICAL_GUIDE_EN.md)** - Details of the optimization algorithms and mathematical models
* **[Optimization Guide](02_OPTIMIZATION_GUIDE_EN.md)** - Implementation techniques behind the "8000x" speedup
* **[Bmson Down-conversion](05_BMSON_CONVERSION_EN.md)** - High-speed down-conversion via memory optimization and caching
* **[Test Strategy](04_TEST_STRATEGY_EN.md)** / **[Commit Guide](COMMIT_MESSAGE_GUIDE_EN.md)** - Development and QA processes

_(Note: You can switch to the Japanese documentation using the language switcher in the header.)_
