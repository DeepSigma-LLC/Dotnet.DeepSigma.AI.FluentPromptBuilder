using DeepSigma.AI.FluentPromptBuilder.Building;
using DeepSigma.AI.FluentPromptBuilder.DependencyInjection;
using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Rendering;
using DeepSigma.AI.FluentPromptBuilder.Repositories;
using DeepSigma.AI.FluentPromptBuilder.Sample;
using Microsoft.Extensions.DependencyInjection;

// 1. Manual fluent build.
var manual = PromptBuilder.Create()
    .System("You are a helpful technical assistant.")
    .User(u => u
        .Section("Task", "Summarize the following error.")
        .Section("Error", "NullReferenceException at MyService.Process(line 42)."))
    .Build();

Utilities.PrintSectionHeader(1);
Utilities.Print("=== Manual prompt — Markdown ===", ConsoleColor.Blue);
Console.WriteLine(new MarkdownPromptRenderer().Render(manual));

Console.WriteLine();
Utilities.Print("=== Manual prompt — JSON ===", ConsoleColor.Blue);
Console.WriteLine(new JsonChatPromptRenderer().Render(manual));

Console.WriteLine();
Utilities.Print("=== Manual prompt — Text ===", ConsoleColor.Blue);
Console.WriteLine(new PlainTextPromptRenderer(PlainTextStyle.ContentOnly).Render(manual));

// 2. Multimodal example: image + tool_call + tool_result.
var multimodal = PromptBuilder.Create()
    .User(u => u
        .Section("Question", "What's in this image?")
        .ImageSection("Photo", new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png"))
    .Assistant(a => a
        .Section("Reply", "Let me look that up.")
        .ToolCallSection("Call", toolCallId: "call_1", toolName: "lookup", argumentsJson: """{"q":"png-header"}"""))
    .Tool(t => t.ToolResultSection(
        "Result", toolCallId: "call_1",
        output: [new TextContent("PNG file signature.")]))
    .Build();


Utilities.PrintSectionHeader(2);
Utilities.Print("=== Multimodal prompt — Chat blocks ===", ConsoleColor.Blue);
foreach (var msg in new ChatMessageRenderer().Render(multimodal))
{
    Console.WriteLine($"[{msg.Role}] ({msg.Content.Count} block{(msg.Content.Count == 1 ? "" : "s")})");
    foreach (var block in msg.Content)
    {
        Console.WriteLine("  " + Utilities.Describe(block));
    }
}

Console.WriteLine();
Utilities.Print("=== Multimodal prompt — JSON ===", ConsoleColor.Blue);
Console.WriteLine(new JsonChatPromptRenderer().Render(multimodal));

Console.WriteLine();
Utilities.Print("=== Multimodal prompt — Text ===", ConsoleColor.Blue);
Console.WriteLine(new PlainTextPromptRenderer(PlainTextStyle.Labeled).Render(multimodal));


// 3. File-loaded template via DI + factory.
var promptsDir = Path.Combine(AppContext.BaseDirectory, "prompts");
var services = new ServiceCollection()
    .AddFluentPromptBuilder()
    .AddFilePromptRepository(promptsDir)
    .BuildServiceProvider();

var factory = services.GetRequiredService<IPromptFactory>();
var stored = await factory.BuildLatestAsync(
    new PromptKey("CodeReview", "SecurityReview"),
    new
    {
        Language = "C#",
        Code = "var pwd = \"hardcoded-secret\";",
    });

Utilities.PrintSectionHeader(3);
Utilities.Print("=== Loaded template — Markdown ===", ConsoleColor.Blue);
Console.WriteLine(services.GetRequiredService<IPromptRenderer<string>>().Render(stored));
Console.WriteLine();
Utilities.Print("=== Loaded template — JSON ===", ConsoleColor.Blue);
Console.WriteLine(new JsonChatPromptRenderer().Render(stored));

Console.WriteLine();
Utilities.Print("=== Loaded template — Text ===", ConsoleColor.Blue);
Console.WriteLine(new PlainTextPromptRenderer(PlainTextStyle.Transcript).Render(manual));


