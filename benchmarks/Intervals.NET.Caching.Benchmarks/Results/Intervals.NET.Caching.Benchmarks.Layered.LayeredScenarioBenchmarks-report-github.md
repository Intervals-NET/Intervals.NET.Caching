```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz (Max: 1.50GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.419
  [Host]     : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4
  Job-CNUJVU : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4

InvocationCount=1  UnrollFactor=1  

```
| Method                       | RangeSpan | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Allocated | Alloc Ratio |
|----------------------------- |---------- |---------:|---------:|---------:|---------:|------:|--------:|----------:|------------:|
| **ColdStart_SwcSwc**             | **100**       | **158.4 μs** |  **5.55 μs** | **15.57 μs** | **159.0 μs** |  **1.01** |    **0.14** |   **18.7 KB** |        **1.00** |
| ColdStart_VpcSwc             | 100       | 137.5 μs |  5.49 μs | 15.58 μs | 131.7 μs |  0.88 |    0.13 |  14.86 KB |        0.79 |
| ColdStart_VpcSwcSwc          | 100       | 180.2 μs |  5.34 μs | 15.06 μs | 176.6 μs |  1.15 |    0.15 |  33.27 KB |        1.78 |
|                              |           |          |          |          |          |       |         |           |             |
| **ColdStart_SwcSwc**             | **1000**      | **429.6 μs** |  **8.37 μs** | **18.19 μs** | **430.6 μs** |  **1.00** |    **0.06** | **113.88 KB** |        **1.00** |
| ColdStart_VpcSwc             | 1000      | 390.7 μs |  7.79 μs | 19.97 μs | 394.4 μs |  0.91 |    0.06 |  92.59 KB |        0.81 |
| ColdStart_VpcSwcSwc          | 1000      | 614.2 μs | 23.61 μs | 69.61 μs | 585.0 μs |  1.43 |    0.17 | 211.88 KB |        1.86 |
|                              |           |          |          |          |          |       |         |           |             |
| **SequentialLocality_SwcSwc**    | **100**       | **194.4 μs** |  **4.55 μs** | **13.05 μs** | **192.7 μs** |  **1.00** |    **0.09** |  **25.09 KB** |        **1.00** |
| SequentialLocality_VpcSwc    | 100       | 188.7 μs |  3.99 μs | 11.25 μs | 187.6 μs |  0.97 |    0.09 |  21.83 KB |        0.87 |
| SequentialLocality_VpcSwcSwc | 100       | 239.2 μs |  8.58 μs | 24.62 μs | 234.8 μs |  1.24 |    0.15 |  42.16 KB |        1.68 |
|                              |           |          |          |          |          |       |         |           |             |
| **SequentialLocality_SwcSwc**    | **1000**      | **468.6 μs** |  **9.30 μs** | **16.53 μs** | **467.6 μs** |  **1.00** |    **0.05** | **121.06 KB** |        **1.00** |
| SequentialLocality_VpcSwc    | 1000      | 441.3 μs |  8.82 μs | 19.54 μs | 436.9 μs |  0.94 |    0.05 |  99.55 KB |        0.82 |
| SequentialLocality_VpcSwcSwc | 1000      | 636.9 μs | 23.97 μs | 70.29 μs | 633.9 μs |  1.36 |    0.16 | 216.82 KB |        1.79 |
