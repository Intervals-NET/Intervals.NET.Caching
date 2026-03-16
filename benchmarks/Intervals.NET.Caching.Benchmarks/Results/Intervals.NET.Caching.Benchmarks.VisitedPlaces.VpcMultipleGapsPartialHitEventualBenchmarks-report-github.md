```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz (Max: 1.50GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.419
  [Host]     : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4
  Job-CNUJVU : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4

InvocationCount=1  UnrollFactor=1  

```
| Method                  | GapCount | MultiGapTotalSegments | StorageStrategy | Mean         | Error      | StdDev      | Median       | Allocated |
|------------------------ |--------- |---------------------- |---------------- |-------------:|-----------:|------------:|-------------:|----------:|
| **PartialHit_MultipleGaps** | **1**        | **1000**                  | **Snapshot**        |     **98.49 μs** |   **6.453 μs** |    **19.03 μs** |     **97.30 μs** |   **2.64 KB** |
| **PartialHit_MultipleGaps** | **1**        | **1000**                  | **LinkedList**      |     **86.43 μs** |   **5.209 μs** |    **14.95 μs** |     **85.80 μs** |   **2.64 KB** |
| **PartialHit_MultipleGaps** | **1**        | **10000**                 | **Snapshot**        |     **56.29 μs** |   **8.486 μs** |    **24.48 μs** |     **50.50 μs** |   **2.64 KB** |
| **PartialHit_MultipleGaps** | **1**        | **10000**                 | **LinkedList**      |     **41.14 μs** |   **5.897 μs** |    **16.92 μs** |     **36.70 μs** |   **2.64 KB** |
| **PartialHit_MultipleGaps** | **10**       | **1000**                  | **Snapshot**        |    **155.91 μs** |   **7.042 μs** |    **20.43 μs** |    **152.90 μs** |  **10.99 KB** |
| **PartialHit_MultipleGaps** | **10**       | **1000**                  | **LinkedList**      |    **158.09 μs** |   **8.684 μs** |    **25.33 μs** |    **154.75 μs** |  **10.99 KB** |
| **PartialHit_MultipleGaps** | **10**       | **10000**                 | **Snapshot**        |     **80.75 μs** |  **10.476 μs** |    **30.06 μs** |     **76.90 μs** |  **10.99 KB** |
| **PartialHit_MultipleGaps** | **10**       | **10000**                 | **LinkedList**      |     **54.56 μs** |   **5.249 μs** |    **15.23 μs** |     **54.85 μs** |  **10.99 KB** |
| **PartialHit_MultipleGaps** | **100**      | **1000**                  | **Snapshot**        |  **1,209.89 μs** |  **86.117 μs** |   **253.92 μs** |  **1,129.05 μs** |  **93.27 KB** |
| **PartialHit_MultipleGaps** | **100**      | **1000**                  | **LinkedList**      |    **611.52 μs** |  **79.679 μs** |   **220.79 μs** |    **478.80 μs** |  **93.27 KB** |
| **PartialHit_MultipleGaps** | **100**      | **10000**                 | **Snapshot**        |    **360.30 μs** |  **23.929 μs** |    **67.88 μs** |    **357.20 μs** |  **93.27 KB** |
| **PartialHit_MultipleGaps** | **100**      | **10000**                 | **LinkedList**      |    **430.45 μs** |  **41.609 μs** |   **120.71 μs** |    **445.50 μs** |  **93.27 KB** |
| **PartialHit_MultipleGaps** | **1000**     | **1000**                  | **Snapshot**        | **23,353.30 μs** | **457.644 μs** |   **801.53 μs** | **23,157.30 μs** | **909.02 KB** |
| **PartialHit_MultipleGaps** | **1000**     | **1000**                  | **LinkedList**      | **24,446.83 μs** | **536.644 μs** | **1,548.34 μs** | **24,088.95 μs** | **909.02 KB** |
| **PartialHit_MultipleGaps** | **1000**     | **10000**                 | **Snapshot**        | **21,471.95 μs** | **949.359 μs** | **2,799.21 μs** | **21,406.80 μs** | **909.02 KB** |
| **PartialHit_MultipleGaps** | **1000**     | **10000**                 | **LinkedList**      | **19,167.83 μs** | **819.234 μs** | **2,415.53 μs** | **19,542.95 μs** | **909.02 KB** |
