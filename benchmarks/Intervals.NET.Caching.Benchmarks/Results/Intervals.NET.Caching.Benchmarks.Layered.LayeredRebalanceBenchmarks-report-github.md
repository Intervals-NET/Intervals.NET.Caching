```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz (Max: 1.50GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.419
  [Host]     : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4
  Job-CNUJVU : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4

InvocationCount=1  UnrollFactor=1  

```
| Method              | BaseSpanSize | Mean      | Error    | StdDev    | Allocated |
|-------------------- |------------- |----------:|---------:|----------:|----------:|
| **Rebalance_SwcSwc**    | **100**          |  **87.59 μs** | **2.921 μs** |  **8.192 μs** |    **7.7 KB** |
| Rebalance_VpcSwc    | 100          |  88.07 μs | 2.649 μs |  7.516 μs |    7.7 KB |
| Rebalance_VpcSwcSwc | 100          |  88.69 μs | 2.642 μs |  7.453 μs |    7.7 KB |
| **Rebalance_SwcSwc**    | **1000**         | **108.52 μs** | **6.406 μs** | **18.688 μs** |    **7.7 KB** |
| Rebalance_VpcSwc    | 1000         | 106.32 μs | 7.431 μs | 21.676 μs |    7.7 KB |
| Rebalance_VpcSwcSwc | 1000         | 110.64 μs | 5.949 μs | 17.260 μs |    7.7 KB |
