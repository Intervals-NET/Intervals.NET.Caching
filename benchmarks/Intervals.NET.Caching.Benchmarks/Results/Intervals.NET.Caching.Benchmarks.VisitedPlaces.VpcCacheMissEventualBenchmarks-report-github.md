```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz (Max: 1.50GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.419
  [Host]     : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4
  Job-CNUJVU : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4

InvocationCount=1  UnrollFactor=1  

```
| Method    | TotalSegments | StorageStrategy | Mean     | Error    | StdDev    | Median   | Allocated |
|---------- |-------------- |---------------- |---------:|---------:|----------:|---------:|----------:|
| **CacheMiss** | **10**            | **Snapshot**        | **17.84 μs** | **1.057 μs** |  **2.965 μs** | **17.40 μs** |     **512 B** |
| **CacheMiss** | **10**            | **LinkedList**      | **16.20 μs** | **0.430 μs** |  **1.148 μs** | **16.00 μs** |     **512 B** |
| **CacheMiss** | **1000**          | **Snapshot**        | **16.61 μs** | **0.930 μs** |  **2.683 μs** | **15.95 μs** |     **512 B** |
| **CacheMiss** | **1000**          | **LinkedList**      | **17.62 μs** | **0.845 μs** |  **2.438 μs** | **16.60 μs** |     **512 B** |
| **CacheMiss** | **100000**        | **Snapshot**        | **37.00 μs** | **5.930 μs** | **17.486 μs** | **26.90 μs** |     **512 B** |
| **CacheMiss** | **100000**        | **LinkedList**      | **24.65 μs** | **0.852 μs** |  **2.198 μs** | **24.60 μs** |     **512 B** |
