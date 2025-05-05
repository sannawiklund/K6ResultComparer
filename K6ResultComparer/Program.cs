using K6ResultAnalyzer;
using ScottPlot.Colormaps;
using System.Xml.Linq;

namespace K6ResultComparer
{
    internal class Program
    {
        //Step 1:
        //    Comment out K6Visualizer and run the program to parse the TXT files to CSV.

        //Step 2:
        //    Comment out K6Parser and run the program to visualize and print the CSV data.

        static void Main(string[] args)
        {
            //K6Parser.ParserMain(args);
            K6Visualizer.VisualizerMain(args);

        }
    }
}
