```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz (Max: 1.50GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.419
  [Host]     : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4
  Job-CNUJVU : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4

InvocationCount=1  UnrollFactor=1  

```
| Method                       | TotalSegments | StorageStrategy | Mean      | Error    | StdDev   | Median    | Allocated |
|----------------------------- |-------------- |---------------- |----------:|---------:|---------:|----------:|----------:|
| **PartialHit_SingleGap_OneHit**  | **1000**          | **Snapshot**        | **101.52 μs** | **8.588 μs** | **24.92 μs** |  **97.90 μs** |   **2.01 KB** |
| PartialHit_SingleGap_TwoHits | 1000          | Snapshot        |  99.85 μs | 8.808 μs | 25.69 μs |  94.30 μs |   2.56 KB |
| **PartialHit_SingleGap_OneHit**  | **1000**          | **LinkedList**      |  **90.77 μs** | **8.170 μs** | **23.70 μs** |  **87.00 μs** |   **2.01 KB** |
| PartialHit_SingleGap_TwoHits | 1000          | LinkedList      | 101.16 μs | 8.554 μs | 24.95 μs | 100.40 μs |   2.56 KB |
| **PartialHit_SingleGap_OneHit**  | **10000**         | **Snapshot**        |  **52.60 μs** | **6.015 μs** | **17.06 μs** |  **45.70 μs** |   **2.01 KB** |
| PartialHit_SingleGap_TwoHits | 10000         | Snapshot        |  49.83 μs | 5.376 μs | 14.99 μs |  44.90 μs |   2.56 KB |
| **PartialHit_SingleGap_OneHit**  | **10000**         | **LinkedList**      |  **44.57 μs** | **5.764 μs** | **16.16 μs** |  **39.75 μs** |   **2.01 KB** |
| PartialHit_SingleGap_TwoHits | 10000         | LinkedList      |  44.40 μs | 4.824 μs | 13.45 μs |  42.55 μs |   2.56 KB |
