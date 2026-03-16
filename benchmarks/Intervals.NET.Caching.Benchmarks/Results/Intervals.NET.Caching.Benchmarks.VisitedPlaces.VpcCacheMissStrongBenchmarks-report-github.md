```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz (Max: 1.50GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.419
  [Host]     : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4
  Job-CNUJVU : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4

InvocationCount=1  UnrollFactor=1  

```
| Method                 | TotalSegments | StorageStrategy | AppendBufferSize | Mean        | Error      | StdDev     | Median      | Allocated |
|----------------------- |-------------- |---------------- |----------------- |------------:|-----------:|-----------:|------------:|----------:|
| **CacheMiss_NoEviction**   | **10**            | **Snapshot**        | **1**                |    **55.10 μs** |   **3.688 μs** |  **10.523 μs** |    **54.45 μs** |    **1992 B** |
| CacheMiss_WithEviction | 10            | Snapshot        | 1                |    61.96 μs |   3.658 μs |  10.556 μs |    60.05 μs |    1464 B |
| **CacheMiss_NoEviction**   | **10**            | **Snapshot**        | **8**                |    **49.80 μs** |   **3.179 μs** |   **9.272 μs** |    **49.65 μs** |    **1984 B** |
| CacheMiss_WithEviction | 10            | Snapshot        | 8                |    66.74 μs |   4.834 μs |  14.100 μs |    65.35 μs |    1352 B |
| **CacheMiss_NoEviction**   | **10**            | **LinkedList**      | **1**                |    **61.27 μs** |   **4.175 μs** |  **12.111 μs** |    **57.50 μs** |    **1136 B** |
| CacheMiss_WithEviction | 10            | LinkedList      | 1                |    77.48 μs |   5.144 μs |  15.005 μs |    75.65 μs |    1432 B |
| **CacheMiss_NoEviction**   | **10**            | **LinkedList**      | **8**                |    **61.67 μs** |   **4.014 μs** |  **11.772 μs** |    **59.70 μs** |    **1048 B** |
| CacheMiss_WithEviction | 10            | LinkedList      | 8                |    73.28 μs |   3.791 μs |  11.177 μs |    69.55 μs |    1400 B |
| **CacheMiss_NoEviction**   | **1000**          | **Snapshot**        | **1**                |   **107.60 μs** |   **5.191 μs** |  **14.726 μs** |   **106.50 μs** |    **9920 B** |
| CacheMiss_WithEviction | 1000          | Snapshot        | 1                |   113.70 μs |   5.121 μs |  14.693 μs |   114.20 μs |    9384 B |
| **CacheMiss_NoEviction**   | **1000**          | **Snapshot**        | **8**                |    **91.67 μs** |   **7.658 μs** |  **22.581 μs** |    **83.25 μs** |    **1000 B** |
| CacheMiss_WithEviction | 1000          | Snapshot        | 8                |    87.94 μs |   9.446 μs |  27.852 μs |    86.05 μs |    1352 B |
| **CacheMiss_NoEviction**   | **1000**          | **LinkedList**      | **1**                |   **147.47 μs** |   **8.151 μs** |  **23.647 μs** |   **145.00 μs** |    **1632 B** |
| CacheMiss_WithEviction | 1000          | LinkedList      | 1                |   146.74 μs |   7.087 μs |  20.897 μs |   140.70 μs |    1928 B |
| **CacheMiss_NoEviction**   | **1000**          | **LinkedList**      | **8**                |   **105.78 μs** |   **7.293 μs** |  **20.924 μs** |   **102.30 μs** |    **1048 B** |
| CacheMiss_WithEviction | 1000          | LinkedList      | 8                |   105.83 μs |   6.551 μs |  18.797 μs |   101.40 μs |    1400 B |
| **CacheMiss_NoEviction**   | **100000**        | **Snapshot**        | **1**                | **2,418.96 μs** |  **48.200 μs** | **110.747 μs** | **2,386.00 μs** |  **801624 B** |
| CacheMiss_WithEviction | 100000        | Snapshot        | 1                | 2,481.24 μs |  49.349 μs | 100.807 μs | 2,458.90 μs |  801384 B |
| **CacheMiss_NoEviction**   | **100000**        | **Snapshot**        | **8**                |   **179.61 μs** |  **17.638 μs** |  **48.285 μs** |   **155.80 μs** |    **1000 B** |
| CacheMiss_WithEviction | 100000        | Snapshot        | 8                |   207.10 μs |  16.461 μs |  45.061 μs |   199.40 μs |    1352 B |
| **CacheMiss_NoEviction**   | **100000**        | **LinkedList**      | **1**                | **4,907.17 μs** |  **97.230 μs** | **165.104 μs** | **4,868.70 μs** |   **51096 B** |
| CacheMiss_WithEviction | 100000        | LinkedList      | 1                | 6,295.23 μs | 147.904 μs | 417.167 μs | 6,191.10 μs |   51432 B |
| **CacheMiss_NoEviction**   | **100000**        | **LinkedList**      | **8**                |   **153.25 μs** |   **9.734 μs** |  **26.646 μs** |   **146.75 μs** |    **1048 B** |
| CacheMiss_WithEviction | 100000        | LinkedList      | 8                |   184.10 μs |  10.880 μs |  29.599 μs |   173.45 μs |    1400 B |
