
using DeepSigma.AI.FluentPromptBuilder.Domain;

namespace DeepSigma.AI.FluentPromptBuilder.Sample;

internal static class Utilities
{
    internal static void PrintSectionHeader(int exampleNumber)
    {
        Console.WriteLine();
        Print("================================", ConsoleColor.Green);
        Print($"=== Example {exampleNumber} ===", ConsoleColor.Green);
        Print("================================", ConsoleColor.Green);
        Console.WriteLine();
    }

    internal static string Truncate(string s, int max) =>
    s.Length <= max ? s : s[..max] + "…";

    internal static string Describe(PromptContent block) => block switch
    {
        TextContent t => $"text: {Utilities.Truncate(t.Text, 70)}",
        ImageContent i => $"image: {i.MediaType}, {i.Data.Length} bytes",
        ToolCallContent c => $"tool_call: {c.ToolName} (id={c.ToolCallId}) args={Utilities.Truncate(c.ArgumentsJson, 60)}",
        ToolResultContent r => $"tool_result: id={r.ToolCallId}, isError={r.IsError}, {r.Output.Count} nested block(s)",
        _ => block.GetType().Name,
    };

    internal static void Print(string text, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(text);
        Console.ResetColor();
    }
}
