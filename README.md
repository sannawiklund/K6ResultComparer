# K6ResultComparer

This program was developed as part of my final thesis project during my studies in .NET development at Teknikhögskolan Gothenburg, Sweden. 

## About the Project

The purpose of this tool is to compare performance test results between two hosting environments for Umbraco CMS:
- **Umbraco Cloud**
- **Umbraco on Microsoft Azure**

Performance tests were conducted using [K6](https://k6.io/), an open source load testing tool by Grafana Labs. Each test scenario targets one of the two hosting solutions and measures key metrics like request duration, failure rate, and throughput.

## How to use

**K6Parser** handles the loading of the raw data from the K6 tests, and then parses the data from txt format to csv format.

When the conversion is complete, **K6Visualizer** handles the loading of the csv files and then compares and visualizes the results.
The comparison is presented directly in the terminal window and the visualized graphs are created and saved in the **K6Plots**-folder.

All raw data can be found in the **Azure With/Without Content** and **Umbraco With/Without Content folders**.

## Thesis

The full written report (in Swedish) will be linked here once it is complete.

> _This project is part of my final examination and is based on real-world testing and comparison of two cloud hosting solutions for Umbraco._

---

Feel free to use and share as needed.

