
BenchmarkDotNet v0.13.12, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.403
  [Host]     : .NET 8.0.11 (8.0.1124.51707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  DefaultJob : .NET 8.0.11 (8.0.1124.51707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI


 Method                | Mean           | Error       | StdDev      | Ratio     | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
---------------------- |---------------:|------------:|------------:|----------:|--------:|-------:|-------:|-------:|----------:|------------:|
 Snapshot_FullHit      |      10.249 μs |   0.2031 μs |   0.2173 μs |      1.00 |    0.00 | 0.9003 | 0.2594 | 0.1526 |   4.96 KB |        1.00 |
 CopyOnRead_FullHit    |       6.390 μs |   0.3221 μs |   0.9136 μs |      0.49 |    0.04 | 0.7401 | 0.2060 |      - |   4.42 KB |        0.89 |
 Snapshot_PartialHit   | 108,990.991 μs | 677.7509 μs | 633.9686 μs | 10,639.80 |  204.88 |      - |      - |      - |    3.7 KB |        0.75 |
 CopyOnRead_PartialHit | 109,094.147 μs | 346.4663 μs | 324.0848 μs | 10,650.38 |  224.92 |      - |      - |      - |    3.7 KB |        0.75 |
 Snapshot_FullMiss     | 109,630.940 μs | 652.3726 μs | 610.2297 μs | 10,702.77 |  231.27 |      - |      - |      - |   5.65 KB |        1.14 |
 CopyOnRead_FullMiss   | 109,447.276 μs | 708.0543 μs | 662.3144 μs | 10,684.51 |  215.23 |      - |      - |      - |   5.65 KB |        1.14 |
