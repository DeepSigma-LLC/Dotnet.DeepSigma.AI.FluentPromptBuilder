
namespace DeepSigma.AI.FluentPromptBuilder.Sample;

internal class Utilities
{
    internal static void PrintSectionHeader(int exampleNumber)
    {
        Console.WriteLine();
        Console.WriteLine("================================");
        Console.WriteLine($"=== Example {exampleNumber} ===");
        Console.WriteLine("================================");
        Console.WriteLine();
    }

    internal static string Truncate(string s, int max) =>
    s.Length <= max ? s : s[..max] + "…";
}
