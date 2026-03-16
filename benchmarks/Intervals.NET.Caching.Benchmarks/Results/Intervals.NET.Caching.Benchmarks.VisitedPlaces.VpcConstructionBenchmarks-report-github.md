```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz (Max: 1.50GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.419
  [Host]     : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4


```
| Method                 | Mean     | Error    | StdDev   | Gen0   | Allocated |
|----------------------- |---------:|---------:|---------:|-------:|----------:|
| Builder_Snapshot       | 757.0 ns | 10.49 ns |  9.30 ns | 0.5865 |    2.4 KB |
| Builder_LinkedList     | 781.8 ns | 12.42 ns | 23.03 ns | 0.5741 |   2.35 KB |
| Constructor_Snapshot   | 674.6 ns | 11.02 ns | 11.32 ns | 0.5026 |   2.05 KB |
| Constructor_LinkedList | 682.1 ns |  6.88 ns |  5.37 ns | 0.4911 |   2.01 KB |
