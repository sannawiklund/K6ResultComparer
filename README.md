# K6ResultComparer

This program was developed as part of my final thesis project during my studies in .NET development. 

## About the Project

The purpose of this tool is to compare performance test results between two hosting environments for Umbraco CMS:
- **Umbraco Cloud**
- **Umbraco on Microsoft Azure**

Performance tests were conducted using [K6](https://k6.io/), an open source load testing tool by Grafana Labs. Each test scenario targets one of the two hosting solutions and measures key metrics like request duration, failure rate, and throughput.

## Test Scenarios

The following types of tests were included:
- **Baseline Test** – standard performance measurement
- **Load Test** – simulating regular traffic
- **Stress Test** – traffic exceeding system capacity
- **Spike Test** – sudden surge in traffic
- **Soak Test** – sustained load over time

Each test was run twice — first on an empty site, then after adding content — to ensure fair and meaningful comparison.

## Implementation

The results are parsed and visualized using C# in a terminal-based application built with .NET. The application generates CSV files and performance charts for further analysis.

## Thesis

The full written report (in Swedish) will be linked here once it is complete.

> _This project is part of my final examination and is based on real-world testing and comparison of two cloud hosting solutions for Umbraco._

---

Feel free to use and share as needed.

