using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ScottPlot; // Make sure to add the ScottPlot NuGet package
using ScottPlot.TickGenerators; // Needed for ScottPlot 5 tick customization
using ScottPlot.Plottables; // Needed for BarPlot specific styling

namespace K6ResultAnalyzer
{
    // Represents a single row from the K6 results CSV
    public class K6Result
    {
        public string Source { get; set; }
        public string File { get; set; } // Test type (Baseline, Load, etc.)
        public string Metric { get; set; }
        public string Avg { get; set; } // Keep as string initially for flexible parsing
        public string Min { get; set; }
        public string Med { get; set; }
        public string Max { get; set; }
        public string P90 { get; set; }
        public string P95 { get; set; }
        public string Value { get; set; }
        public string Rate { get; set; } // Keep as string initially
        public string Percentage { get; set; } // Keep as string initially
        public string Count { get; set; }
        public string Total { get; set; }

        // Helper method to parse duration strings (e.g., "25.53ms", "1.05s") into milliseconds
        public double? ParseDurationToMs(string durationStr)
        {
            if (string.IsNullOrWhiteSpace(durationStr)) return null;

            durationStr = durationStr.Trim();
            double value;

            if (durationStr.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(durationStr.Substring(0, durationStr.Length - 2), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                {
                    return value;
                }
            }
            else if (durationStr.EndsWith("µs", StringComparison.OrdinalIgnoreCase) || durationStr.EndsWith("us", StringComparison.OrdinalIgnoreCase))
            {
                // Handle potential different unicode chars for microsecond
                string numPart = durationStr.Contains("µs")
                    ? durationStr.Substring(0, durationStr.Length - 2)
                    : durationStr.Substring(0, durationStr.Length - 2);

                if (double.TryParse(numPart, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                {
                    return value / 1000.0; // Convert microseconds to milliseconds
                }
            }
            else if (durationStr.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(durationStr.Substring(0, durationStr.Length - 1), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                {
                    return value * 1000.0; // Convert seconds to milliseconds
                }
            }
            else // Assume it might be just a number (potentially seconds if no unit)
            {
                if (double.TryParse(durationStr, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                {
                    // Ambiguous without unit, but often K6 defaults might imply seconds for some metrics if unitless
                    // Or maybe it's already ms? Let's assume ms if no unit is present for duration-like fields.
                    // Adjust this assumption if needed based on your specific K6 output.
                    return value;
                }
            }
            Console.WriteLine($"Warning: Could not parse duration '{durationStr}'");
            return null;
        }

        // Helper method to parse rate strings (e.g., "5.71704/s")
        public double? ParseRate(string rateStr)
        {
            if (string.IsNullOrWhiteSpace(rateStr)) return null;
            rateStr = rateStr.Trim();
            if (rateStr.EndsWith("/s", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(rateStr.Substring(0, rateStr.Length - 2), NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                {
                    return value;
                }
            }
            Console.WriteLine($"Warning: Could not parse rate '{rateStr}'");
            return null;
        }

        // Helper method to parse percentage strings (e.g., "0.00%")
        public double? ParsePercentage(string percStr)
        {
            if (string.IsNullOrWhiteSpace(percStr)) return null;
            percStr = percStr.Trim();
            if (percStr.EndsWith("%", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(percStr.Substring(0, percStr.Length - 1), NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
                {
                    return value;
                }
            }
            Console.WriteLine($"Warning: Could not parse percentage '{percStr}'");
            return null;
        }
    }

    class K6Visualizer
    {
        public static void VisualizerMain(string[] args)
        {
            // --- Configuration ---
            // IMPORTANT: Update this path to your actual CSV file location
            string csvFilePath = @"C:\Users\sanna\Documents\Examensarbete\K6ResultComparer\K6ResultComparer\k6_comparison_results_csharp.csv"; // Use verbatim string literal @ for paths
            string outputDirectory = "K6Plots";
            // ---------------------

            // Basic check if the path looks like a placeholder
            if (csvFilePath.Contains("path/to/your") || csvFilePath.Contains("path\\to\\your"))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: Please update the 'csvFilePath' variable in Program.cs to the actual path of your CSV file.");
                Console.ResetColor();
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return;
            }


            Console.WriteLine($"Reading K6 results from: {csvFilePath}");

            List<K6Result> results = LoadK6Results(csvFilePath);

            if (results == null || !results.Any())
            {
                Console.WriteLine("No results loaded or error reading file. Exiting.");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($"Loaded {results.Count} data points.");

            // Ensure output directory exists
            Directory.CreateDirectory(outputDirectory);
            Console.WriteLine($"Saving plots to: {Path.GetFullPath(outputDirectory)}"); // Show full path


            // --- Generate Plots ---
            var testTypes = results.Select(r => r.File).Distinct().OrderBy(f => f);
            var sources = results.Select(r => r.Source).Distinct().OrderBy(s => s);

            foreach (var testType in testTypes)
            {
                Console.WriteLine($"\n--- Analyzing Test Type: {testType} ---");

                // Filter results for the current test type
                var testTypeResults = results.Where(r => r.File == testType).ToList();

                if (!testTypeResults.Any()) continue;

                // 1. Plot: Average HTTP Request Duration (ms)
                GenerateBarPlot(
                    data: testTypeResults,
                    metricName: "http_req_duration",
                    valueSelector: r => r.ParseDurationToMs(r.Avg),
                    sources: sources.ToList(),
                    title: $"Avg HTTP Req Duration ({testType})",
                    yLabel: "Avg Duration (ms)",
                    filePath: Path.Combine(outputDirectory, $"AvgReqDuration_{testType}.png")
                );

                // 2. Plot: P95 HTTP Request Duration (ms)
                GenerateBarPlot(
                    data: testTypeResults,
                    metricName: "http_req_duration",
                    valueSelector: r => r.ParseDurationToMs(r.P95),
                    sources: sources.ToList(),
                    title: $"P95 HTTP Req Duration ({testType})",
                    yLabel: "P95 Duration (ms)",
                    filePath: Path.Combine(outputDirectory, $"P95ReqDuration_{testType}.png")
                );

                // 3. Plot: HTTP Request Failure Rate (%)
                GenerateBarPlot(
                   data: testTypeResults,
                   metricName: "http_req_failed",
                   valueSelector: r => r.ParsePercentage(r.Percentage),
                   sources: sources.ToList(),
                   title: $"HTTP Req Failure Rate ({testType})",
                   yLabel: "Failure Rate (%)",
                   filePath: Path.Combine(outputDirectory, $"FailureRate_{testType}.png")
               );

                // 4. Plot: HTTP Requests Rate (/s)
                GenerateBarPlot(
                   data: testTypeResults,
                   metricName: "http_reqs",
                   valueSelector: r => r.ParseRate(r.Rate),
                   sources: sources.ToList(),
                   title: $"HTTP Reqs Rate ({testType})",
                   yLabel: "Requests per Second (/s)",
                   filePath: Path.Combine(outputDirectory, $"ReqRate_{testType}.png")
               );

                // --- Basic Textual Summary (Example) ---
                Console.WriteLine($"\nBasic Summary for {testType}:");
                var saved_values = new Dictionary<string, Dictionary<string, double>>();
                var saved_string_values = new Dictionary<string, Dictionary<string, string>>(); // 
                foreach (var source in sources)
                {
                    var sourceResults = testTypeResults.Where(r => r.Source == source); // Filter results for this specific source

                    // Find the specific metric rows for this source
                    var durationMetric = sourceResults.FirstOrDefault(r => r.Metric == "http_req_duration");
                    var failureMetric = sourceResults.FirstOrDefault(r => r.Metric == "http_req_failed");
                    var reqsMetric = sourceResults.FirstOrDefault(r => r.Metric == "http_reqs");
                    var waitingMetric = sourceResults.FirstOrDefault(r => r.Metric == "http_req_waiting");
                    var receivingMetric = sourceResults.FirstOrDefault(r => r.Metric == "http_req_receiving");
                    var sendingMetric = sourceResults.FirstOrDefault(r => r.Metric == "http_req_sending");
                    var iterationDurationMetric = sourceResults.FirstOrDefault(r => r.Metric == "iteration_duration");
                    var iterationsMetric = sourceResults.FirstOrDefault(r => r.Metric == "iterations");
                    var vusMaxMetric = sourceResults.FirstOrDefault(r => r.Metric == "vus_max");

                    // Safely parse the values if the metric row was found
                    double? avgDuration = durationMetric?.ParseDurationToMs(durationMetric.Avg);
                    double? p95Duration = durationMetric?.ParseDurationToMs(durationMetric.P95);
                    double? failRate = failureMetric?.ParsePercentage(failureMetric.Percentage);
                    double? reqRate = reqsMetric?.ParseRate(reqsMetric.Rate);
                    double? waitAvg = waitingMetric?.ParseDurationToMs(waitingMetric.Avg);
                    double? waitP95 = waitingMetric?.ParseDurationToMs(waitingMetric.P95);
                    double? recvAvg = receivingMetric?.ParseDurationToMs(receivingMetric.Avg);
                    double? recvP95 = receivingMetric?.ParseDurationToMs(receivingMetric.P95);
                    double? sendAvg = sendingMetric?.ParseDurationToMs(sendingMetric.Avg);
                    double? sendP95 = sendingMetric?.ParseDurationToMs(sendingMetric.P95);



                    saved_values[source] = new Dictionary<string, double>();
                    saved_values[source]["avg"] = avgDuration.GetValueOrDefault(0);
                    saved_values[source]["p95"] = p95Duration.GetValueOrDefault(0);
                    saved_values[source]["failRate"] = failRate.GetValueOrDefault(0);
                    saved_values[source]["reqRate"] = reqRate.GetValueOrDefault(0);
                    saved_values[source]["waitAvg"] = waitAvg.GetValueOrDefault(0);
                    saved_values[source]["waitP95"] = waitP95.GetValueOrDefault(0);
                    saved_values[source]["recvAvg"] = recvAvg.GetValueOrDefault(0);
                    saved_values[source]["recvP95"] = recvP95.GetValueOrDefault(0);
                    saved_values[source]["sendAvg"] = sendAvg.GetValueOrDefault(0);
                    saved_values[source]["sendP95"] = sendP95.GetValueOrDefault(0);




                    // Handle the string metrics for iterations and vus_max
                    string vusMax = vusMaxMetric?.Value ?? "N/A";
                    string iterations = iterationsMetric?.Value ?? "N/A";

                    saved_values[source]["sendP95"] = sendP95.GetValueOrDefault(0);

                    // Lägg till parsade string-metrics som double om det går
                    if (double.TryParse(iterations, NumberStyles.Any, CultureInfo.InvariantCulture, out double iterationsVal))
                    {
                        saved_values[source]["iterations"] = iterationsVal;
                    }
                    if (double.TryParse(vusMax, NumberStyles.Any, CultureInfo.InvariantCulture, out double vusMaxVal))
                    {
                        saved_values[source]["vusMax"] = vusMaxVal;
                    }


                    saved_string_values[source] = new Dictionary<string, string>();
                    saved_string_values[source]["iterations"] = iterations;
                    saved_string_values[source]["vusMax"] = vusMax;




                    // Print the results
                    Console.WriteLine($"  Source: {source}");
                    Console.WriteLine($"    Avg Req Duration: {avgDuration?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A"} ms");
                    Console.WriteLine($"    P95 Req Duration: {p95Duration?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A"} ms");
                    Console.WriteLine($"    Failure Rate: {failRate?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A"} %");
                    Console.WriteLine($"    Request Rate: {reqRate?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A"} /s");
                    Console.WriteLine($"    Waiting Avg: {waitAvg?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A"} ms");
                    Console.WriteLine($"    Waiting P95: {waitP95?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A"} ms");
                    Console.WriteLine($"    Receiving Avg: {recvAvg?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A"} ms");
                    Console.WriteLine($"    Receiving P95: {recvP95?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A"} ms");
                    Console.WriteLine($"    Sending Avg: {sendAvg?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A"} ms");
                    Console.WriteLine($"    Sending P95: {sendP95?.ToString("F2", CultureInfo.InvariantCulture) ?? "N/A"} ms");
                    Console.WriteLine($"    Iterations: {saved_string_values[source]["iterations"]}");
                    Console.WriteLine($"    VUs Max: {saved_string_values[source]["vusMax"]}");

                }

                var no_content = saved_values.Where(v => v.Key.Contains("With")).ToDictionary();

                var keys = no_content.First().Value.Keys.ToList();
                Console.WriteLine($"{string.Join(", ", no_content.Keys.ToArray())}");
                Console.WriteLine("\n--- Jämförelser mellan 'Without Content'-resultat ---");

                foreach (var metric in keys)
                {
                    var sorted = no_content
                        .OrderBy(kvp => kvp.Value[metric])
                        .ToList();

                    var best = sorted.First();
                    var worst = sorted.Last();
                    double difference = worst.Value[metric] - best.Value[metric];

                    string unit = metric switch
                    {
                        "avg" or "p95" => "ms",
                        "failRate" => "%",
                        "reqRate" => "/s",
                        "blockedAvg" or "blockedP95" => "ms",
                        "connectAvg" or "connectP95" => "ms",
                        "tlsAvg" => "ms",
                        "waitAvg" or "waitP95" => "ms",
                        "recvAvg" or "recvP95" => "ms", // alt: "MB/s" om det är throughput
                        "sendAvg" or "sendP95" => "ms", // alt: "MB/s"
                        "iterations" => "st",
                        "vusMax" => "st",
                        _ => ""
                    };

                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  {metric}:");
                    Console.WriteLine($"    Bäst: {best.Key} ({best.Value[metric]:F2} {unit})");
                    Console.WriteLine($"    Sämst: {worst.Key} ({worst.Value[metric]:F2} {unit})");
                    Console.WriteLine($"    Skillnad: {difference:F2} {unit}");
                    Console.ResetColor();
                }


            }


            Console.WriteLine("\nAnalysis complete. Plots saved.");
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();



        }





        // Reads the CSV file and returns a list of K6Result objects
        public static List<K6Result> LoadK6Results(string filePath)
        {
            var results = new List<K6Result>();
            if (!File.Exists(filePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: CSV file not found at '{filePath}'");
                Console.ResetColor();
                return null;
            }

            try
            {
                // Read all lines, skip header
                var lines = File.ReadAllLines(filePath).Skip(1);
                int lineNum = 1; // Start from line 2 (after header)

                foreach (var line in lines)
                {
                    lineNum++;
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    // Simple split, assumes no commas within fields.
                    // Consider using a library like CsvHelper for complex CSVs.
                    var columns = line.Split(',');

                    if (columns.Length < 14) // Check for expected number of columns
                    {
                        Console.WriteLine($"Warning: Skipping malformed line #{lineNum} (expected 14 columns, got {columns.Length}): {line}");
                        continue;
                    }

                    // Basic validation/cleaning could be added here
                    results.Add(new K6Result
                    {
                        Source = columns[0].Trim(),
                        File = columns[1].Trim(),
                        Metric = columns[2].Trim(),
                        Avg = columns[3].Trim(),
                        Min = columns[4].Trim(),
                        Med = columns[5].Trim(),
                        Max = columns[6].Trim(),
                        P90 = columns[7].Trim(),
                        P95 = columns[8].Trim(),
                        Value = columns[9].Trim(),
                        Rate = columns[10].Trim(),
                        Percentage = columns[11].Trim(),
                        Count = columns[12].Trim(),
                        Total = columns[13].Trim()
                    });
                }
            }
            catch (IOException ioEx) // Catch specific IO errors
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR reading file '{filePath}': {ioEx.Message}");
                Console.ResetColor();
                return null;
            }
            catch (Exception ex) // Catch other potential errors during processing
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error processing CSV file: {ex.Message}");
                Console.ResetColor();
                return null; // Indicate failure
            }

            return results;
        }

        public static void GenerateBarPlot(
            List<K6Result> data,
            string metricName,
            Func<K6Result, double?> valueSelector,
            List<string> sources,
            string title,
            string yLabel,
            string filePath)
        {
            var plot = new Plot();

            var labels = new List<Tick>();
            bool dataFound = false;

            // List to track bar data by category (e.g. Azure, Umbraco)
            var azurePositions = new List<double>();
            var azureValues = new List<double>();

            var umbracoPositions = new List<double>();
            var umbracoValues = new List<double>();

            var fallbackPositions = new List<double>();
            var fallbackValues = new List<double>();

            for (int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                var metricData = data.FirstOrDefault(r => r.Source == source && r.Metric == metricName);
                double? value = metricData != null ? valueSelector(metricData) : null;

                double barValue = 0;
                string labelText = $"{source} (N/A)";

                if (value.HasValue)
                {
                    barValue = value.Value;
                    labelText = source;
                    dataFound = true;
                }
                else
                {
                    Console.WriteLine($"Warning: No data found for Metric='{metricName}', Source='{source}' in plot '{title}'");
                }

                labels.Add(new Tick(i, labelText));

                // Add to corresponding color group
                if (source.Contains("Azure", StringComparison.OrdinalIgnoreCase))
                {
                    azurePositions.Add(i);
                    azureValues.Add(barValue);
                }
                else if (source.Contains("Umbraco", StringComparison.OrdinalIgnoreCase))
                {
                    umbracoPositions.Add(i);
                    umbracoValues.Add(barValue);
                }
                else
                {
                    fallbackPositions.Add(i);
                    fallbackValues.Add(barValue);
                }
            }

            if (!dataFound)
            {
                Console.WriteLine($"Skipping plot '{title}' - No data found for metric '{metricName}'.");
                return;
            }

            // Add separate bar series for each group
            if (azurePositions.Count > 0)
            {
                var azureBars = plot.Add.Bars(azurePositions.ToArray(), azureValues.ToArray());
                azureBars.Color = Colors.CornflowerBlue;
                azureBars.ValueLabelStyle.OffsetY = -5;
                azureBars.ValueLabelStyle.Bold = true;
                azureBars.ValueLabelStyle.ForeColor = Colors.Black;
            }

            if (umbracoPositions.Count > 0)
            {
                var umbracoBars = plot.Add.Bars(umbracoPositions.ToArray(), umbracoValues.ToArray());
                umbracoBars.Color = Color.FromHex("#2A4B8D"); // mörkare blå
                umbracoBars.ValueLabelStyle.OffsetY = -5;
                umbracoBars.ValueLabelStyle.Bold = true;
                umbracoBars.ValueLabelStyle.ForeColor = Colors.Black;
            }

            if (fallbackPositions.Count > 0)
            {
                var fallbackBars = plot.Add.Bars(fallbackPositions.ToArray(), fallbackValues.ToArray());
                fallbackBars.Color = Colors.Gray;
                fallbackBars.ValueLabelStyle.OffsetY = -5;
                fallbackBars.ValueLabelStyle.Bold = true;
                fallbackBars.ValueLabelStyle.ForeColor = Colors.Black;
            }

            // X-etiketter
            plot.Axes.Bottom.TickGenerator = new NumericManual(labels.ToArray());
            plot.Axes.Bottom.TickLabelStyle.Rotation = 0;
            plot.Axes.Bottom.TickLabelStyle.Alignment = Alignment.LowerCenter;

            plot.Axes.Bottom.Label.Text = "Source (PaaS Provider)";
            plot.Axes.Left.Label.Text = yLabel;
            plot.Axes.Title.Label.Text = title;

            plot.Axes.AutoScale();

            try
            {
                plot.Save(filePath, 800, 600);
                Console.WriteLine($"  - Saved plot: {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error saving plot '{filePath}': {ex.Message}");
                Console.ResetColor();
            }
        }

    }
}
