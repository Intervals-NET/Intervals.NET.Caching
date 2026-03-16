```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz (Max: 1.50GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.419
  [Host]     : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4
  Job-CNUJVU : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4

InvocationCount=1  UnrollFactor=1  

```
| Method                  | GapCount | MultiGapTotalSegments | StorageStrategy | AppendBufferSize | Mean        | Error     | StdDev      | Median      | Allocated  |
|------------------------ |--------- |---------------------- |---------------- |----------------- |------------:|----------:|------------:|------------:|-----------:|
| **PartialHit_MultipleGaps** | **1**        | **1000**                  | **Snapshot**        | **1**                |    **212.1 μs** |  **19.32 μs** |    **56.35 μs** |    **211.1 μs** |      **11 KB** |
| **PartialHit_MultipleGaps** | **1**        | **1000**                  | **Snapshot**        | **8**                |    **190.4 μs** |  **15.77 μs** |    **46.26 μs** |    **196.6 μs** |    **3.16 KB** |
| **PartialHit_MultipleGaps** | **1**        | **1000**                  | **LinkedList**      | **1**                |    **220.3 μs** |  **12.50 μs** |    **36.26 μs** |    **216.9 μs** |    **3.72 KB** |
| **PartialHit_MultipleGaps** | **1**        | **1000**                  | **LinkedList**      | **8**                |    **191.3 μs** |  **19.45 μs** |    **57.04 μs** |    **183.8 μs** |     **3.2 KB** |
| **PartialHit_MultipleGaps** | **1**        | **10000**                 | **Snapshot**        | **1**                |    **216.2 μs** |   **7.18 μs** |    **19.53 μs** |    **216.0 μs** |   **81.31 KB** |
| **PartialHit_MultipleGaps** | **1**        | **10000**                 | **Snapshot**        | **8**                |    **217.1 μs** |  **24.90 μs** |    **73.03 μs** |    **190.3 μs** |    **3.16 KB** |
| **PartialHit_MultipleGaps** | **1**        | **10000**                 | **LinkedList**      | **1**                |    **580.5 μs** |  **20.44 μs** |    **58.97 μs** |    **567.2 μs** |    **8.12 KB** |
| **PartialHit_MultipleGaps** | **1**        | **10000**                 | **LinkedList**      | **8**                |    **189.9 μs** |  **23.22 μs** |    **67.73 μs** |    **193.9 μs** |     **3.2 KB** |
| **PartialHit_MultipleGaps** | **10**       | **1000**                  | **Snapshot**        | **1**                |    **309.1 μs** |  **13.50 μs** |    **38.09 μs** |    **306.9 μs** |   **22.13 KB** |
| **PartialHit_MultipleGaps** | **10**       | **1000**                  | **Snapshot**        | **8**                |    **285.9 μs** |  **23.22 μs** |    **67.75 μs** |    **271.6 μs** |   **22.13 KB** |
| **PartialHit_MultipleGaps** | **10**       | **1000**                  | **LinkedList**      | **1**                |    **271.1 μs** |  **21.34 μs** |    **62.24 μs** |    **260.4 μs** |    **15.2 KB** |
| **PartialHit_MultipleGaps** | **10**       | **1000**                  | **LinkedList**      | **8**                |    **318.0 μs** |  **18.44 μs** |    **52.91 μs** |    **315.0 μs** |    **15.2 KB** |
| **PartialHit_MultipleGaps** | **10**       | **10000**                 | **Snapshot**        | **1**                |    **246.3 μs** |  **17.67 μs** |    **51.56 μs** |    **243.1 μs** |   **92.44 KB** |
| **PartialHit_MultipleGaps** | **10**       | **10000**                 | **Snapshot**        | **8**                |    **319.5 μs** |  **25.29 μs** |    **72.98 μs** |    **304.8 μs** |   **92.44 KB** |
| **PartialHit_MultipleGaps** | **10**       | **10000**                 | **LinkedList**      | **1**                |    **630.9 μs** |  **24.52 μs** |    **71.14 μs** |    **614.1 μs** |   **19.59 KB** |
| **PartialHit_MultipleGaps** | **10**       | **10000**                 | **LinkedList**      | **8**                |    **583.0 μs** |  **21.24 μs** |    **60.59 μs** |    **576.8 μs** |   **19.59 KB** |
| **PartialHit_MultipleGaps** | **100**      | **1000**                  | **Snapshot**        | **1**                |  **1,342.9 μs** |  **69.43 μs** |   **201.43 μs** |  **1,361.0 μs** |  **128.43 KB** |
| **PartialHit_MultipleGaps** | **100**      | **1000**                  | **Snapshot**        | **8**                |  **1,154.3 μs** | **143.70 μs** |   **419.17 μs** |  **1,129.2 μs** |  **128.43 KB** |
| **PartialHit_MultipleGaps** | **100**      | **1000**                  | **LinkedList**      | **1**                |    **789.6 μs** | **108.02 μs** |   **316.81 μs** |    **605.1 μs** |  **125.06 KB** |
| **PartialHit_MultipleGaps** | **100**      | **1000**                  | **LinkedList**      | **8**                |  **1,365.3 μs** |  **45.07 μs** |   **130.77 μs** |  **1,343.2 μs** |  **125.06 KB** |
| **PartialHit_MultipleGaps** | **100**      | **10000**                 | **Snapshot**        | **1**                |    **593.0 μs** |  **11.64 μs** |    **20.39 μs** |    **591.5 μs** |  **198.74 KB** |
| **PartialHit_MultipleGaps** | **100**      | **10000**                 | **Snapshot**        | **8**                |    **624.6 μs** |  **38.16 μs** |   **108.88 μs** |    **611.5 μs** |  **198.74 KB** |
| **PartialHit_MultipleGaps** | **100**      | **10000**                 | **LinkedList**      | **1**                |    **954.9 μs** |  **20.42 μs** |    **58.92 μs** |    **952.5 μs** |  **129.46 KB** |
| **PartialHit_MultipleGaps** | **100**      | **10000**                 | **LinkedList**      | **8**                |  **1,012.4 μs** |  **28.40 μs** |    **81.95 μs** |  **1,004.0 μs** |  **129.46 KB** |
| **PartialHit_MultipleGaps** | **1000**     | **1000**                  | **Snapshot**        | **1**                | **24,570.8 μs** | **482.47 μs** | **1,262.53 μs** | **24,264.8 μs** | **1247.85 KB** |
| **PartialHit_MultipleGaps** | **1000**     | **1000**                  | **Snapshot**        | **8**                | **23,970.8 μs** | **476.95 μs** | **1,066.76 μs** | **23,796.2 μs** | **1247.84 KB** |
| **PartialHit_MultipleGaps** | **1000**     | **1000**                  | **LinkedList**      | **1**                | **22,295.5 μs** | **441.07 μs** | **1,207.43 μs** | **21,917.1 μs** | **1280.08 KB** |
| **PartialHit_MultipleGaps** | **1000**     | **1000**                  | **LinkedList**      | **8**                | **24,404.6 μs** | **534.95 μs** | **1,455.37 μs** | **24,151.7 μs** | **1280.08 KB** |
| **PartialHit_MultipleGaps** | **1000**     | **10000**                 | **Snapshot**        | **1**                | **20,650.0 μs** | **401.93 μs** | **1,107.02 μs** | **20,484.5 μs** | **1246.55 KB** |
| **PartialHit_MultipleGaps** | **1000**     | **10000**                 | **Snapshot**        | **8**                | **21,947.2 μs** | **435.51 μs** | **1,009.35 μs** | **21,899.0 μs** | **1246.55 KB** |
| **PartialHit_MultipleGaps** | **1000**     | **10000**                 | **LinkedList**      | **1**                | **20,479.7 μs** | **366.66 μs** |   **592.08 μs** | **20,304.0 μs** | **1212.86 KB** |
| **PartialHit_MultipleGaps** | **1000**     | **10000**                 | **LinkedList**      | **8**                | **20,814.2 μs** | **409.63 μs** |   **872.95 μs** | **20,696.8 μs** | **1212.86 KB** |
