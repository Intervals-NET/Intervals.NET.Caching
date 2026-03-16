```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz (Max: 1.50GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.419
  [Host]     : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4


```
| Method                 | Mean     | Error     | StdDev    | Gen0   | Allocated |
|----------------------- |---------:|----------:|----------:|-------:|----------:|
| Construction_SwcSwc    | 1.054 μs | 0.0206 μs | 0.0237 μs | 1.0071 |   4.12 KB |
| Construction_VpcSwc    | 1.347 μs | 0.0263 μs | 0.0303 μs | 1.1196 |   4.58 KB |
| Construction_VpcSwcSwc | 1.784 μs | 0.0356 μs | 0.0424 μs | 1.5831 |   6.47 KB |
