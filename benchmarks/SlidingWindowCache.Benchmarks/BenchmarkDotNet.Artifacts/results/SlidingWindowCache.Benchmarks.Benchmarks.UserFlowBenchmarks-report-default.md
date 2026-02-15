
BenchmarkDotNet v0.13.12, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz, 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.403
  [Host] : .NET 8.0.11 (8.0.1124.51707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  Dry    : .NET 8.0.11 (8.0.1124.51707), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=Dry  IterationCount=1  LaunchCount=1  
RunStrategy=ColdStart  UnrollFactor=1  WarmupCount=1  

 Method                | RangeSpan | CacheCoefficientSize | Mean      | Error | Ratio | Allocated | Alloc Ratio |
---------------------- |---------- |--------------------- |----------:|------:|------:|----------:|------------:|
 **User_FullHit_Snapshot** | **100**       | **1**                    |  **2.640 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **100**       | **10**                   |  **2.167 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **100**       | **100**                  |  **2.283 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **100**       | **1000**                 |  **2.671 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **1000**      | **1**                    |  **1.823 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **1000**      | **10**                   |  **2.139 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **1000**      | **100**                  |  **2.250 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **1000**      | **1000**                 |  **1.853 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **10000**     | **1**                    |  **1.893 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **10000**     | **10**                   |  **1.860 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **10000**     | **100**                  |  **2.068 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **10000**     | **1000**                 |  **2.107 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **100000**    | **1**                    |  **2.689 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **100000**    | **10**                   |  **2.164 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **100000**    | **100**                  |  **2.203 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **100000**    | **1000**                 |  **2.517 ms** |    **NA** |  **1.00** |   **1.48 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **1000000**   | **1**                    |  **1.882 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **1000000**   | **10**                   |  **2.014 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **1000000**   | **100**                  |  **2.358 ms** |    **NA** |  **1.00** |   **1.77 KB** |        **1.00** |
                       |           |                      |           |       |       |           |             |
 **User_FullHit_Snapshot** | **1000000**   | **1000**                 | **49.120 ms** |    **NA** |  **1.00** |   **2.39 KB** |        **1.00** |
