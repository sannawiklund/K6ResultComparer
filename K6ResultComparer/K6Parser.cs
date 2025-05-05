using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

public class K6Parser
{
    // Regex patterns translated from Python (using .NET syntax for named groups)
    // Note: Adjusted patterns slightly for C# string literals and regex engine nuances.
    private static readonly Regex MetricPattern = new Regex(
        @"^(?<metric>[\w_]+(?:\{.*\})?)\.*:\s+" +
        @"avg=(?<avg>[\w\.\sμ%]+)\s+" +
        @"min=(?<min>[\w\.\sμ%]+)\s+" +
        @"med=(?<med>[\w\.\sμ%]+)\s+" +
        @"max=(?<max>[\w\.\sμ%]+)\s+" +
        @"p\(90\)=(?<p90>[\w\.\sμ%]+)\s+" +
        @"p\(95\)=(?<p95>[\w\.\sμ%]+)",
        RegexOptions.Compiled);

    private static readonly Regex ValueRatePattern = new Regex(
        @"^(?<metric>[\w_]+)\.*:\s+" +
        @"(?<value>[\d\.]+)\s+" +
        @"(?<rate>[\d\.]+)/s",
        RegexOptions.Compiled);

    private static readonly Regex SingleValuePattern = new Regex(
        @"^(?<metric>[\w_]+)\.*:\s+" +
        @"(?<value>[\d\.\s]+\w+(?:/s)?)", // Capture number, space, unit (optional /s)
        RegexOptions.Compiled);

    private static readonly Regex PercentagePattern = new Regex(
        @"^(?<metric>[\w_]+)\.*:\s+" +
        @"(?<percentage>[\d\.]+%)\s+" +
        @"(?<count>\d+)\s+out of\s+(?<total>\d+)",
        RegexOptions.Compiled);

    private static readonly Regex VusPattern = new Regex(
        @"^(?<metric>vus(?:_max)?)\.*:\s+" +
        @"(?<value>\d+)\s+" +
        @"min=(?<min>\d+)\s+" +
        @"max=(?<max>\d+)",
        RegexOptions.Compiled);


    /// <summary>
    /// Parses a single k6 output file and extracts metrics.
    /// </summary>
    /// <param name="filePath">The path to the k6 output file.</param>
    /// <returns>A dictionary where keys are metric names and values are dictionaries of stats.</returns>
    public static Dictionary<string, Dictionary<string, string>> ParseK6Output(string filePath)
    {
        var metrics = new Dictionary<string, Dictionary<string, string>>();

        try
        {
            // Read all lines from the file
            string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

            foreach (string lineRaw in lines)
            {
                string line = lineRaw.Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                Match match;

                // Try matching patterns in order of specificity
                match = MetricPattern.Match(line);
                if (match.Success)
                {
                    string metricName = match.Groups["metric"].Value.Split('{')[0].Trim('.'); // Clean metric name
                    metrics[metricName] = new Dictionary<string, string>
                    {
                        { "Avg", match.Groups["avg"].Value.Trim() },
                        { "Min", match.Groups["min"].Value.Trim() },
                        { "Med", match.Groups["med"].Value.Trim() },
                        { "Max", match.Groups["max"].Value.Trim() },
                        { "P90", match.Groups["p90"].Value.Trim() },
                        { "P95", match.Groups["p95"].Value.Trim() }
                    };
                    continue; // Move to next line once matched
                }

                match = PercentagePattern.Match(line);
                if (match.Success)
                {
                    string metricName = match.Groups["metric"].Value.Trim('.');
                    metrics[metricName] = new Dictionary<string, string>
                    {
                        { "Percentage", match.Groups["percentage"].Value.Trim() },
                        { "Count", match.Groups["count"].Value.Trim() },
                        { "Total", match.Groups["total"].Value.Trim() }
                    };
                    continue;
                }

                match = VusPattern.Match(line);
                if (match.Success)
                {
                    string metricName = match.Groups["metric"].Value.Trim('.');
                    metrics[metricName] = new Dictionary<string, string>
                    {
                        { "Value", match.Groups["value"].Value.Trim() },
                        { "Min", match.Groups["min"].Value.Trim() },
                        { "Max", match.Groups["max"].Value.Trim() }
                    };
                    continue;
                }

                match = ValueRatePattern.Match(line);
                if (match.Success)
                {
                    string metricName = match.Groups["metric"].Value.Trim('.');
                    metrics[metricName] = new Dictionary<string, string>
                     {
                         { "Value", match.Groups["value"].Value.Trim() },
                         { "Rate", match.Groups["rate"].Value.Trim() + "/s" }
                     };
                    continue;
                }

                match = SingleValuePattern.Match(line);
                if (match.Success)
                {
                    string metricName = match.Groups["metric"].Value.Trim('.');
                    // Avoid overwriting if already parsed by a more specific pattern
                    if (!metrics.ContainsKey(metricName))
                    {
                        metrics[metricName] = new Dictionary<string, string>
                        {
                            { "Value", match.Groups["value"].Value.Trim() }
                        };
                    }
                    continue;
                }
                // Optional: Log lines that didn't match any pattern for debugging
                // Console.WriteLine($"Unmatched line in {Path.GetFileName(filePath)}: {line}");
            }
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Error: File not found - {filePath}");
            return null; // Indicate error
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error reading file {filePath}: {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
            return null; // Indicate error
        }

        return metrics;
    }

    /// <summary>
    /// Writes data to a CSV file.
    /// </summary>
    /// <param name="filePath">Path to the output CSV file.</param>
    /// <param name="data">List of data rows (dictionaries).</param>
    /// <param name="headers">List of headers in the desired order.</param>
    private static void WriteCsv(string filePath, List<Dictionary<string, string>> data, List<string> headers)
    {
        try
        {
            using (var writer = new StreamWriter(filePath, false, Encoding.UTF8)) // Overwrite if exists
            {
                // Write header row
                writer.WriteLine(string.Join(",", headers.Select(h => QuoteCsvField(h))));

                // Write data rows
                foreach (var row in data)
                {
                    var values = headers.Select(header =>
                        row.TryGetValue(header, out var value) ? QuoteCsvField(value) : ""
                    );
                    writer.WriteLine(string.Join(",", values));
                }
            }
            Console.WriteLine($"Successfully wrote results to {filePath}");
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Error writing CSV file {filePath}: {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred during CSV writing: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper to quote CSV fields if they contain commas, quotes, or newlines.
    /// </summary>
    private static string QuoteCsvField(string field)
    {
        if (field == null) return "";
        // Simple quoting: if field contains comma, quote, or newline, enclose in double quotes
        // and double up any existing double quotes.
        if (field.Contains(",") || field.Contains("\"") || field.Contains("\n") || field.Contains("\r"))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }
        return field;
    }


    /// <summary>
    /// Main entry point of the application.
    /// </summary>
    /// <param name="args">Command-line arguments (not used in this basic version).</param>
    public static void ParserMain(string[] args)
    {
        // --- Configuration ---
        // List of folders containing your k6 output .txt files
        // Assumes this runs from the parent directory of 'azure' and 'cloud'
        var foldersToProcess = new List<string> { "C:\\Users\\sanna\\Documents\\Examensarbete\\K6ResultComparer\\K6ResultComparer\\Azure With Content\\", "C:\\Users\\sanna\\Documents\\Examensarbete\\K6ResultComparer\\K6ResultComparer\\Azure Without Content\\", "C:\\Users\\sanna\\Documents\\Examensarbete\\K6ResultComparer\\K6ResultComparer\\Cloud With Content\\", "C:\\Users\\sanna\\Documents\\Examensarbete\\K6ResultComparer\\K6ResultComparer\\Cloud Without Content\\" };
        // Name of the output CSV file
        string outputCsvFile = "k6_comparison_results_csharp.csv";
        // ---------------------

        // Very basic argument handling (replace with a library like System.CommandLine for robust parsing)
        if (args.Length > 0 && args[0] == "-f" && args.Length > 1)
        {
            foldersToProcess = args.Skip(1).TakeWhile(a => a != "-o").ToList();
        }
        if (args.Length > 0 && args.Contains("-o") && args.ToList().IndexOf("-o") < args.Length - 1)
        {
            outputCsvFile = args[args.ToList().IndexOf("-o") + 1];
        }


        var allData = new List<Dictionary<string, string>>();
        int processedFilesCount = 0;

        foreach (string folderPath in foldersToProcess)
        {
            //string sourceName = Path.GetFileName(folderPath); // Use folder name as source
            DirectoryInfo dirInfo = new DirectoryInfo(folderPath);
            string sourceName = dirInfo.Name;
            Console.WriteLine($"Processing folder: {folderPath} (Source: {sourceName})");

            if (!Directory.Exists(folderPath))
            {
                Console.WriteLine($"Warning: Folder not found - {folderPath}. Skipping.");
                continue;
            }

            try
            {
                // Process only .txt files
                foreach (string filePath in Directory.EnumerateFiles(folderPath, "*.txt"))
                {
                    string fileName = Path.GetFileName(filePath);
                    Console.WriteLine($"  Parsing file: {fileName}");
                    var parsedMetrics = ParseK6Output(filePath);

                    if (parsedMetrics != null)
                    {
                        processedFilesCount++;
                        foreach (var kvp in parsedMetrics)
                        {
                            string metricName = kvp.Key;
                            Dictionary<string, string> values = kvp.Value;

                            var row = new Dictionary<string, string>
                            {
                                { "Source", sourceName },
                                { "File", fileName.Replace("Azure-", "").Replace("Cloud-", "").Replace(".txt", "") },
                                { "Metric", metricName }
                            };

                            // Add all the parsed stat values for this metric
                            foreach (var stat in values)
                            {
                                // Key is already capitalized in ParseK6Output (e.g., "Avg", "Min")
                                row[stat.Key] = stat.Value;
                            }
                            allData.Add(row);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"  Skipping file due to parsing errors: {fileName}");
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"Error: Access denied to folder {folderPath}. {ex.Message}");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error accessing folder {folderPath}. {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unexpected error occurred processing folder {folderPath}: {ex.Message}");
            }
        }

        if (allData.Count == 0)
        {
            Console.WriteLine("No data parsed. Exiting.");
            return;
        }

        Console.WriteLine($"\nProcessed {processedFilesCount} files.");

        // --- CSV Header Generation ---
        // Define a preferred order, placing common stats first
        var preferredHeaders = new List<string> { "Source", "File", "Metric", "Avg", "Min", "Med", "Max", "P90", "P95", "Value", "Rate", "Percentage", "Count", "Total" };

        // Get all unique keys found in the data
        var allKeys = new HashSet<string>(allData.SelectMany(d => d.Keys));

        // Start with preferred headers that actually exist in the data
        var finalHeaders = preferredHeaders.Where(h => allKeys.Contains(h)).ToList();

        // Add any remaining keys found in the data, sorted alphabetically
        finalHeaders.AddRange(allKeys.Except(preferredHeaders).OrderBy(k => k));
        // --- End CSV Header Generation ---


        // Write the collected data to the CSV file
        WriteCsv(outputCsvFile, allData, finalHeaders);

        Console.WriteLine("Processing complete.");
    }
}