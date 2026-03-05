# BMS Part Tuner: Technical Portfolio Highlights

**Executive Summary for Recruiters & Engineers**

This project is not just a utility tool; it is a demonstration of **high-performance computing**, **modern .NET architecture**, and **AI-orchestrated development**.

## **1\. Optimization Engineering (The "800x" Logic)**

**Source:** [02_OPTIMIZATION\_GUIDE.md](https://raw.githubusercontent.com/bms-atelier-kyokufu/BmsPartTuner/refs/heads/main/docs/02_OPTIMIZATION_GUIDE.md)

I achieved an **800x performance boost** (1 hour → 3 seconds) by resolving critical bottlenecks in file I/O and audio processing.

* **Bottleneck Analysis:** Identified severe latency in System.IO file access and naive byte-array comparisons.  
* **Key Techniques:**  
  * **Memory-Mapped Files:** Zero-copy access to large audio datasets.  
  * **SIMD / Vectorization:** Utilized hardware intrinsics for parallel wave data comparison.  
  * **Algorithmic Overhaul:** Replaced $O(N^2)$ comparison logic with an optimized hashing strategy.

* ##  2. System Architecture (Scalability & Maintainability)

**Source:** [00_ARCHITECTURE.md](https://raw.githubusercontent.com/bms-atelier-kyokufu/BmsPartTuner/refs/heads/main/docs/00_ARCHITECTURE_EN.md)

Designed with **Clean Architecture** principles to ensure the application is testable, maintainable, and scalable.

* **Layered Design:** Strict separation between Core (Domain), Infrastructure (Services), and UI (WPF/MVVM).  
* **Modern Stack:** Built on **.NET 10**, leveraging **CommunityToolkit.Mvvm** for efficient state management and **Dependency Injection** for loose coupling.

## **3\. Quality Assurance Strategy**

**Source:** [04_TEST\_STRATEGY.md](https://raw.githubusercontent.com/bms-atelier-kyokufu/BmsPartTuner/refs/heads/main/docs/04_TEST_STRATEGY.md)

Quality is not an afterthought. I implemented a robust testing pyramid to guarantee stability.

* **Mutation Testing:** Used Roslyn-based mutation tools to ensure tests actually catch bugs.  
* **Property-Based Testing:** Automated edge-case generation to validate logic against unexpected inputs.  
* **AI-Assisted QA:** Integrated Gemini to generate test cases for complex optimization logic.
