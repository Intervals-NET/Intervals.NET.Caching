```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz (Max: 1.50GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.419
  [Host]     : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4
  Job-CNUJVU : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4

InvocationCount=1  UnrollFactor=1  

```
| Method                       | TotalSegments | StorageStrategy | AppendBufferSize | Mean     | Error    | StdDev   | Median   | Allocated |
|----------------------------- |-------------- |---------------- |----------------- |---------:|---------:|---------:|---------:|----------:|
| **PartialHit_SingleGap_OneHit**  | **1000**          | **Snapshot**        | **1**                | **213.9 μs** | **19.74 μs** | **57.88 μs** | **203.7 μs** |  **10.35 KB** |
| PartialHit_SingleGap_TwoHits | 1000          | Snapshot        | 1                | 204.6 μs | 18.29 μs | 52.77 μs | 204.2 μs |  10.91 KB |
| **PartialHit_SingleGap_OneHit**  | **1000**          | **Snapshot**        | **8**                | **178.3 μs** | **18.56 μs** | **54.74 μs** | **163.2 μs** |   **2.51 KB** |
| PartialHit_SingleGap_TwoHits | 1000          | Snapshot        | 8                | 189.6 μs | 18.24 μs | 53.22 μs | 192.5 μs |   3.06 KB |
| **PartialHit_SingleGap_OneHit**  | **1000**          | **LinkedList**      | **1**                | **220.4 μs** | **15.34 μs** | **44.73 μs** | **216.5 μs** |   **3.07 KB** |
| PartialHit_SingleGap_TwoHits | 1000          | LinkedList      | 1                | 234.6 μs | 17.52 μs | 51.39 μs | 239.2 μs |   3.63 KB |
| **PartialHit_SingleGap_OneHit**  | **1000**          | **LinkedList**      | **8**                | **187.5 μs** | **18.28 μs** | **53.91 μs** | **193.5 μs** |   **2.55 KB** |
| PartialHit_SingleGap_TwoHits | 1000          | LinkedList      | 8                | 199.4 μs | 16.71 μs | 49.27 μs | 201.9 μs |   3.11 KB |
| **PartialHit_SingleGap_OneHit**  | **10000**         | **Snapshot**        | **1**                | **296.0 μs** | **31.31 μs** | **89.82 μs** | **262.7 μs** |  **80.66 KB** |
| PartialHit_SingleGap_TwoHits | 10000         | Snapshot        | 1                | 214.8 μs | 10.65 μs | 30.23 μs | 204.4 μs |  81.22 KB |
| **PartialHit_SingleGap_OneHit**  | **10000**         | **Snapshot**        | **8**                | **204.0 μs** | **19.89 μs** | **58.02 μs** | **192.5 μs** |   **2.51 KB** |
| PartialHit_SingleGap_TwoHits | 10000         | Snapshot        | 8                | 206.4 μs | 19.06 μs | 54.38 μs | 189.5 μs |   3.06 KB |
| **PartialHit_SingleGap_OneHit**  | **10000**         | **LinkedList**      | **1**                | **580.9 μs** | **24.09 μs** | **68.74 μs** | **559.1 μs** |   **7.47 KB** |
| PartialHit_SingleGap_TwoHits | 10000         | LinkedList      | 1                | 592.8 μs | 24.66 μs | 71.53 μs | 574.5 μs |   8.02 KB |
| **PartialHit_SingleGap_OneHit**  | **10000**         | **LinkedList**      | **8**                | **196.5 μs** | **22.10 μs** | **64.82 μs** | **212.0 μs** |   **2.55 KB** |
| PartialHit_SingleGap_TwoHits | 10000         | LinkedList      | 8                | 201.2 μs | 23.32 μs | 68.03 μs | 220.3 μs |   3.11 KB |
