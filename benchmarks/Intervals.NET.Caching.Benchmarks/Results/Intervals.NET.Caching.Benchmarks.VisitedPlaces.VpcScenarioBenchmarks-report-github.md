```

BenchmarkDotNet v0.15.8, Windows 10 (10.0.19045.6456/22H2/2022Update)
Intel Core i7-1065G7 CPU 1.30GHz (Max: 1.50GHz), 1 CPU, 8 logical and 4 physical cores
.NET SDK 8.0.419
  [Host]     : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4
  Job-CNUJVU : .NET 8.0.25 (8.0.25, 8.0.2526.11203), X64 RyuJIT x86-64-v4

InvocationCount=1  UnrollFactor=1  

```
| Method             | BurstSize | StorageStrategy | SchedulingStrategy | Mean        | Error     | StdDev     | Median      | Allocated |
|------------------- |---------- |---------------- |------------------- |------------:|----------:|-----------:|------------:|----------:|
| **Scenario_AllHits**   | **10**        | **Snapshot**        | **Unbounded**          |    **70.17 μs** |  **4.694 μs** |  **13.316 μs** |    **66.20 μs** |   **14.2 KB** |
| **Scenario_AllHits**   | **10**        | **Snapshot**        | **Bounded**            |    **67.41 μs** |  **3.867 μs** |  **10.844 μs** |    **65.50 μs** |   **12.8 KB** |
| **Scenario_AllHits**   | **10**        | **LinkedList**      | **Unbounded**          |    **63.27 μs** |  **2.712 μs** |   **7.824 μs** |    **61.50 μs** |  **14.13 KB** |
| **Scenario_AllHits**   | **10**        | **LinkedList**      | **Bounded**            |    **65.87 μs** |  **3.037 μs** |   **8.567 μs** |    **64.70 μs** |  **12.87 KB** |
| **Scenario_AllHits**   | **50**        | **Snapshot**        | **Unbounded**          |   **205.21 μs** |  **4.052 μs** |   **6.308 μs** |   **205.25 μs** |  **73.13 KB** |
| **Scenario_AllHits**   | **50**        | **Snapshot**        | **Bounded**            |   **210.88 μs** |  **4.041 μs** |   **4.654 μs** |   **211.40 μs** |  **67.27 KB** |
| **Scenario_AllHits**   | **50**        | **LinkedList**      | **Unbounded**          |   **221.80 μs** |  **4.394 μs** |   **7.696 μs** |   **221.30 μs** |  **72.76 KB** |
| **Scenario_AllHits**   | **50**        | **LinkedList**      | **Bounded**            |   **217.01 μs** |  **4.055 μs** |   **4.164 μs** |   **217.10 μs** |   **66.3 KB** |
| **Scenario_AllHits**   | **100**       | **Snapshot**        | **Unbounded**          |   **406.28 μs** |  **8.056 μs** |  **21.363 μs** |   **398.25 μs** | **146.51 KB** |
| **Scenario_AllHits**   | **100**       | **Snapshot**        | **Bounded**            |   **417.56 μs** |  **8.141 μs** |  **14.043 μs** |   **414.05 μs** | **133.98 KB** |
| **Scenario_AllHits**   | **100**       | **LinkedList**      | **Unbounded**          |   **410.44 μs** |  **8.099 μs** |  **17.777 μs** |   **403.90 μs** | **147.26 KB** |
| **Scenario_AllHits**   | **100**       | **LinkedList**      | **Bounded**            |   **409.13 μs** |  **7.837 μs** |   **8.711 μs** |   **407.70 μs** | **133.51 KB** |
|                    |           |                 |                    |             |           |            |             |           |
| **Scenario_Churn**     | **10**        | **Snapshot**        | **Unbounded**          |   **121.50 μs** |  **3.261 μs** |   **9.199 μs** |   **119.55 μs** |  **10.79 KB** |
| **Scenario_Churn**     | **10**        | **Snapshot**        | **Bounded**            |   **125.28 μs** |  **3.755 μs** |  **10.713 μs** |   **123.85 μs** |   **9.46 KB** |
| **Scenario_Churn**     | **10**        | **LinkedList**      | **Unbounded**          |   **179.41 μs** |  **3.564 μs** |   **8.469 μs** |   **177.60 μs** |  **11.18 KB** |
| **Scenario_Churn**     | **10**        | **LinkedList**      | **Bounded**            |   **183.92 μs** |  **3.642 μs** |   **7.681 μs** |   **182.45 μs** |   **9.85 KB** |
| **Scenario_Churn**     | **50**        | **Snapshot**        | **Unbounded**          |   **485.93 μs** |  **9.565 μs** |  **21.591 μs** |   **482.60 μs** |  **54.77 KB** |
| **Scenario_Churn**     | **50**        | **Snapshot**        | **Bounded**            |   **456.30 μs** |  **9.012 μs** |  **18.612 μs** |   **456.65 μs** |  **60.88 KB** |
| **Scenario_Churn**     | **50**        | **LinkedList**      | **Unbounded**          |   **679.41 μs** | **13.584 μs** |  **23.067 μs** |   **677.40 μs** |  **54.91 KB** |
| **Scenario_Churn**     | **50**        | **LinkedList**      | **Bounded**            |   **678.45 μs** | **13.299 μs** |  **25.623 μs** |   **677.35 μs** |  **62.15 KB** |
| **Scenario_Churn**     | **100**       | **Snapshot**        | **Unbounded**          | **1,028.04 μs** | **46.664 μs** | **136.121 μs** |   **980.05 μs** | **114.76 KB** |
| **Scenario_Churn**     | **100**       | **Snapshot**        | **Bounded**            |   **877.48 μs** | **17.399 μs** |  **26.571 μs** |   **874.00 μs** | **131.48 KB** |
| **Scenario_Churn**     | **100**       | **LinkedList**      | **Unbounded**          | **1,309.35 μs** | **24.864 μs** |  **45.465 μs** | **1,312.60 μs** |  **109.9 KB** |
| **Scenario_Churn**     | **100**       | **LinkedList**      | **Bounded**            | **1,330.28 μs** | **25.711 μs** |  **39.263 μs** | **1,325.00 μs** | **129.24 KB** |
|                    |           |                 |                    |             |           |            |             |           |
| **Scenario_ColdStart** | **10**        | **Snapshot**        | **Unbounded**          |    **58.78 μs** |  **2.457 μs** |   **6.849 μs** |    **57.55 μs** |   **7.33 KB** |
| **Scenario_ColdStart** | **10**        | **Snapshot**        | **Bounded**            |    **64.08 μs** |  **3.976 μs** |  **11.407 μs** |    **61.90 μs** |   **6.29 KB** |
| **Scenario_ColdStart** | **10**        | **LinkedList**      | **Unbounded**          |    **76.03 μs** |  **5.618 μs** |  **16.210 μs** |    **71.20 μs** |   **7.74 KB** |
| **Scenario_ColdStart** | **10**        | **LinkedList**      | **Bounded**            |    **65.06 μs** |  **3.470 μs** |   **9.674 μs** |    **63.10 μs** |    **6.7 KB** |
| **Scenario_ColdStart** | **50**        | **Snapshot**        | **Unbounded**          |   **152.26 μs** |  **5.986 μs** |  **16.980 μs** |   **146.60 μs** |  **36.51 KB** |
| **Scenario_ColdStart** | **50**        | **Snapshot**        | **Bounded**            |   **136.95 μs** |  **3.288 μs** |   **9.001 μs** |   **135.30 μs** |  **31.05 KB** |
| **Scenario_ColdStart** | **50**        | **LinkedList**      | **Unbounded**          |   **199.80 μs** |  **5.343 μs** |  **14.804 μs** |   **197.00 μs** |  **37.63 KB** |
| **Scenario_ColdStart** | **50**        | **LinkedList**      | **Bounded**            |   **191.79 μs** |  **3.799 μs** |  **10.400 μs** |   **189.40 μs** |  **32.46 KB** |
| **Scenario_ColdStart** | **100**       | **Snapshot**        | **Unbounded**          |   **259.65 μs** |  **7.176 μs** |  **19.644 μs** |   **253.15 μs** |  **74.98 KB** |
| **Scenario_ColdStart** | **100**       | **Snapshot**        | **Bounded**            |   **238.80 μs** |  **4.333 μs** |   **8.653 μs** |   **237.60 μs** |  **64.76 KB** |
| **Scenario_ColdStart** | **100**       | **LinkedList**      | **Unbounded**          |   **374.63 μs** | **13.421 μs** |  **37.412 μs** |   **359.25 μs** |  **75.12 KB** |
| **Scenario_ColdStart** | **100**       | **LinkedList**      | **Bounded**            |   **363.46 μs** |  **5.605 μs** |   **7.288 μs** |   **361.90 μs** |  **73.15 KB** |
