using DeepSigma.AI.FluentPromptBuilder.Building;
using DeepSigma.AI.FluentPromptBuilder.DependencyInjection;
using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Rendering;
using DeepSigma.AI.FluentPromptBuilder.Repositories;
using Microsoft.Extensions.DependencyInjection;

// 1. Manual fluent build.
var manual = PromptBuilder.Create()
    .System("You are a helpful technical assistant.")
    .User(u => u
        .Section("Task", "Summarize the following error.")
        .Section("Error", "NullReferenceException at MyService.Process(line 42)."))
    .Build();

PrintSectionHeader(1);
Console.WriteLine("=== Manual prompt — Markdown ===");
Console.WriteLine(new MarkdownPromptRenderer().Render(manual));

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


PrintSectionHeader(2);
Console.WriteLine("=== Multimodal prompt — Chat blocks ===");
foreach (var msg in new ChatMessageRenderer().Render(multimodal))
{
    Console.WriteLine($"[{msg.Role}] ({msg.Content.Count} block{(msg.Content.Count == 1 ? "" : "s")})");
    foreach (var block in msg.Content)
    {
        Console.WriteLine("  " + Describe(block));
    }
}

static string Describe(ChatContentBlock block) => block switch
{
    ChatTextBlock t       => $"text: {Truncate(t.Text, 70)}",
    ChatImageBlock i      => $"image: {i.MediaType}, {i.Data.Length} bytes",
    ChatToolCallBlock c   => $"tool_call: {c.ToolName} (id={c.ToolCallId}) args={Truncate(c.ArgumentsJson, 60)}",
    ChatToolResultBlock r => $"tool_result: id={r.ToolCallId}, isError={r.IsError}, {r.Output.Count} nested block(s)",
    _                     => block.GetType().Name,
};

static string Truncate(string s, int max) =>
    s.Length <= max ? s : s[..max] + "…";

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

PrintSectionHeader(3);
Console.WriteLine("=== Loaded template — Markdown ===");
Console.WriteLine(services.GetRequiredService<IPromptRenderer<string>>().Render(stored));


static void PrintSectionHeader(int exampleNumber)
{
    Console.WriteLine("================================");
    Console.WriteLine($"=== Example {exampleNumber} ===");
    Console.WriteLine("================================");
    Console.WriteLine();
}
