# BMS Part Tuner: Test Strategy

## Overview

This document outlines the testing strategy for the BMS Part Tuner project. **Goal** : Maintain a practical, robust test suite that is highly sustainable within a 1-person-month maintenance effort.

## Testing Philosophy

### Core Logic-Centric Approach

Testing in this project strictly focuses on the **correctness of the business logic** .

Tests for supplementary UI features or simple property getters/setters have been intentionally omitted to maximize the ROI of our testing efforts.

### The Test Pyramid

```
        /\
       /  \      Integration Tests (Scenarios)
      /____\     
     /      \    Domain Logic (Core, Services)
    /________\   
   /          \  Utilities (Helpers, Converters)
  /____________\ 

```

## Mandatory Tests (Whitelist)

The following tests are **strictly prohibited from deletion** . Do not modify them unless the underlying core logic is explicitly changing.

### 1. Core Optimization Engine

| Test File | Purpose | Priority |
| --- | --- | --- |
| `Core/Optimization/SimulationEngineTests.cs` | Validates threshold and reduction count calculation logic | ⭐⭐⭐ |
| `Core/Helpers/AudioFileGroupingStrategyTests.cs` | Audio file grouping accuracy *(Prevents data corruption)* | ⭐⭐⭐ |

### 2. BMS Data Operations & Calculations

| Test File | Purpose | Priority |
| --- | --- | --- |
| `Core/Bms/BmsFileRewriterTests.cs` | Regex logic for definition number replacement *(Prevents data corruption)* | ⭐⭐⭐ |
| `Core/Bms/BmsFileRewriterTests_Atomic.cs` | Guarantees atomicity of file writing operations | ⭐⭐⭐ |
| `Core/Bms/DefinitionRangeManagerTests.cs` | Boundary value testing for definition number range calculations | ⭐⭐ |
| `Core/Bms/DefinitionStatisticsTests.cs` | Accuracy of statistical data computations | ⭐⭐ |
| `Core/Helpers/RadixConvertTests.cs` | Correctness of Base36/Base62 conversions | ⭐⭐ |

### 3. Input Validation

| Test File | Purpose | Priority |
| --- | --- | --- |
| `Core/Validation/BmsValidatorsTests.cs` | Boundary and anomalous value testing for user inputs | ⭐⭐ |

### 4. Audio Processing

| Test File | Purpose | Priority |
| --- | --- | --- |
| `Audio/ParallelAudioComparisonEngineTests.cs` | Correctness of the parallel audio comparison engine | ⭐⭐⭐ |
| `Audio/FastWaveCompareTests.cs` | Accuracy of the audio match detection logic | ⭐⭐ |
| `Audio/WaveValidationTests.cs` | Mathematical correctness of correlation coefficient calculations | ⭐⭐ |
| `Audio/AudioCacheManagerTests.cs` | Consistency of cache management | ⭐⭐ |

### 5. Service Layer

| Test File | Purpose | Priority |
| --- | --- | --- |
| `Services/BmsOptimizationServiceTests.cs` | Integration tests for the optimization process | ⭐⭐⭐ |
| `Services/BmsOptimizationServiceTests_Deletion.cs` | Safety of physical file deletion operations | ⭐⭐⭐ |
| `Services/AudioPreviewServiceTests.cs` | Verification of audio preview functionality | ⭐ |
| `Services/ResultCardServiceTests.cs` | Consistency of result display logic | ⭐ |

### 6. ViewModels (State Transitions Only)

| Test File | Purpose | Priority |
| --- | --- | --- |
| `ViewModels/OptimizationViewModelTests.cs` | State transitions during optimization execution | ⭐⭐ |
| `ViewModels/OptimizationViewModelTests_SlideState.cs` | State management for the slide confirmation UI | ⭐⭐ |

### 7. Utilities

| Test File | Purpose | Priority |
| --- | --- | --- |
| `Converters/CorrelationCoefficientConverterTests.cs` | Accuracy of correlation coefficient display conversions | ⭐ |
| `Core/AppConstantsTests.cs` | Integrity of constant values | ⭐ |

## Integration Tests

| Test File | Purpose | Priority |
| --- | --- | --- |
| `Scenarios/OptimizationScenarioTests.cs` | End-to-end simulation (Fully in-memory) | ⭐⭐⭐ | **Crucial** : This test deliberately bypasses physical file I/O. It generates audio data in-memory to ensure high-speed execution.

## Mutation Testing

Tests under the `RoslynMutation/` directory will be **maintained as-is** .

We retain these to quantitatively measure code robustness, hardware resources and CI execution limits permitting.

## Metric Targets

| Metric | Target | Current Status |
| --- | --- | --- |
| Test File Count | 20 - 30 files | ~25 files |
| CI Execution Time | < 5 mins | TBD |
| Core Logic Coverage | > 90% | TBD |
| UI Layer Coverage | > 30% | TBD |

## Maintenance Policy

### Criteria for Adding Tests

When introducing new features, tests must be added if they meet any of the following criteria:

1. **High Data Corruption Risk** : File I/O operations, definition number replacements, etc.
2. **Complex Calculations** : Correlation coefficients, statistical processing algorithms, etc.
3. **Boundary/Edge Cases** : Input validations, range boundary checks, etc.
4. **Complex VM State Transitions** : ViewModels that directly encapsulate complex business logic flows.

### Criteria for Omitting/Removing Tests

Consider omitting or removing tests that fall into these anti-patterns for our specific strategy:

1. **Testing 3rd-Party Libraries** : Validating standard libraries or frameworks (e.g., `CommunityToolkit.Mvvm`).
2. **Trivial Getters/Setters** : Properties without underlying business logic.
3. **Pure `PropertyChanged` Verifications** : This is the responsibility of the MVVM framework, not our domain tests.
4. **Supplementary UI Logic** : Simple filtering, sorting, or basic display format conversions.

## Development Workflow

1. **Feature Additions** : Only add tests if the feature impacts the core domain logic.
2. **Refactoring** : Ensure all existing tests in the whitelist pass successfully.
3. **Bug Fixes** : Add regression tests to prevent recurrence (strictly adhering to the "Criteria for Adding Tests").
4. **Periodic Review** : Reassess the test suite composition and strategy on a quarterly basis.