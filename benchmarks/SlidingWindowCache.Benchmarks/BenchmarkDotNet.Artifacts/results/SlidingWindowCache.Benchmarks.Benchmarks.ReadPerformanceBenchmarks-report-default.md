
BenchmarkDotNet v0.13.12, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.403
  [Host] : .NET 8.0.11 (8.0.1124.51707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Dry    : .NET 8.0.11 (8.0.1124.51707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=Dry  IterationCount=1  LaunchCount=1  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=1  

 Method                  | Mean     | Error | Ratio | Allocated | Alloc Ratio |
------------------------ |---------:|------:|------:|----------:|------------:|
 Snapshot_FullCacheHit   | 1.638 ms |    NA |  1.00 |   6.09 KB |        1.00 |
 CopyOnRead_FullCacheHit | 3.924 ms |    NA |  2.40 |   6.09 KB |        1.00 |
