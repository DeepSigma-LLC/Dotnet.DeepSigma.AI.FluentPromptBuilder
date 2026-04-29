using DeepSigma.AI.FluentPromptBuilder.Building;
using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Rendering;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Tests.Rendering;

public class MarkdownPromptRendererTests
{
    [Fact]
    public void Render_TextSections_EmitsHeadings()
    {
        var prompt = PromptBuilder.Create()
            .System("You are helpful.")
            .User(u => u.Section("Task", "Hi"))
            .Build();

        var md = new MarkdownPromptRenderer().Render(prompt);

        Assert.Contains("## System", md, StringComparison.Ordinal);
        Assert.Contains("### System", md, StringComparison.Ordinal);
        Assert.Contains("You are helpful.", md, StringComparison.Ordinal);
        Assert.Contains("## User", md, StringComparison.Ordinal);
        Assert.Contains("### Task", md, StringComparison.Ordinal);
        Assert.Contains("Hi", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_SmallImage_EmitsDataUri()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var prompt = PromptBuilder.Create()
            .User(u => u.ImageSection("Photo", bytes, "image/png"))
            .Build();

        var md = new MarkdownPromptRenderer().Render(prompt);
        Assert.Contains("data:image/png;base64,", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_LargeImage_EmitsPlaceholder()
    {
        var bytes = new byte[100]; // larger than threshold of 32
        var prompt = PromptBuilder.Create()
            .User(u => u.ImageSection("Photo", bytes, "image/png"))
            .Build();

        var md = new MarkdownPromptRenderer(largeImageThreshold: 32).Render(prompt);
        Assert.Contains("[image: image/png, 100 bytes]", md, StringComparison.Ordinal);
        Assert.DoesNotContain("data:image/png;base64,", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ToolCallAndToolResult_EmitFencedBlocks()
    {
        var prompt = PromptBuilder.Create()
            .Assistant(a => a.ToolCallSection("Call", "call_1", "lookup", "{\"id\":42}"))
            .Tool(t => t.ToolResultSection("Result", "call_1", [new TextContent("found")]))
            .Build();

        var md = new MarkdownPromptRenderer().Render(prompt);
        Assert.Contains("```json", md, StringComparison.Ordinal);
        Assert.Contains("\"name\":\"lookup\"", md, StringComparison.Ordinal);
        Assert.Contains("\"tool_call_id\":\"call_1\"", md, StringComparison.Ordinal);
        // arguments preserved as a nested JSON object (raw, since input parsed)
        Assert.Contains("\"arguments\":{\"id\":42}", md, StringComparison.Ordinal);
        Assert.Contains("```tool-result", md, StringComparison.Ordinal);
        Assert.Contains("tool_call_id: call_1", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_ToolCall_WithValidJsonArgs_EmitsValidJsonWrapper()
    {
        var prompt = PromptBuilder.Create()
            .Assistant(a => a.ToolCallSection("Call", "c1", "lookup", "{\"id\":42}"))
            .Build();

        var md = new MarkdownPromptRenderer().Render(prompt);

        // Extract the fenced JSON payload and confirm it parses.
        var start = md.IndexOf("```json", StringComparison.Ordinal);
        var fenceContentStart = md.IndexOf('\n', start) + 1;
        var fenceContentEnd = md.IndexOf("```", fenceContentStart, StringComparison.Ordinal);
        var jsonPayload = md[fenceContentStart..fenceContentEnd].Trim();

        using var doc = System.Text.Json.JsonDocument.Parse(jsonPayload);
        Assert.Equal("c1", doc.RootElement.GetProperty("tool_call_id").GetString());
        Assert.Equal("lookup", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal(42, doc.RootElement.GetProperty("arguments").GetProperty("id").GetInt32());
    }

    [Fact]
    public void Render_ToolCall_WithInvalidJsonArgs_FallsBackToQuotedString()
    {
        var prompt = PromptBuilder.Create()
            .Assistant(a => a.ToolCallSection("Call", "c1", "lookup", "not json"))
            .Build();

        var md = new MarkdownPromptRenderer().Render(prompt);

        var start = md.IndexOf("```json", StringComparison.Ordinal);
        var fenceContentStart = md.IndexOf('\n', start) + 1;
        var fenceContentEnd = md.IndexOf("```", fenceContentStart, StringComparison.Ordinal);
        var jsonPayload = md[fenceContentStart..fenceContentEnd].Trim();

        // The wrapper must still parse; arguments becomes a quoted string.
        using var doc = System.Text.Json.JsonDocument.Parse(jsonPayload);
        Assert.Equal("not json", doc.RootElement.GetProperty("arguments").GetString());
    }
}

public class EmptySectionSuppressionTests
{
    private static BuiltPrompt PromptWith(params PromptMessage[] messages) =>
        new(Source: null, messages);

    [Fact]
    public void Markdown_SkipsSectionsWithEmptyText()
    {
        var prompt = PromptWith(new PromptMessage(PromptRole.User,
        [
            new PromptSection("Filled",   new TextContent("hello"), 0),
            new PromptSection("Empty",    new TextContent(""), 1),
            new PromptSection("Spaces",   new TextContent("   \t\n"), 2),
        ]));

        var md = new MarkdownPromptRenderer().Render(prompt);

        Assert.Contains("### Filled", md, StringComparison.Ordinal);
        Assert.DoesNotContain("### Empty", md, StringComparison.Ordinal);
        Assert.DoesNotContain("### Spaces", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Markdown_OmitsMessageEntirelyWhenAllSectionsAreEmpty()
    {
        var prompt = PromptWith(
            new PromptMessage(PromptRole.System,
                [new PromptSection("OptionalNotes", new TextContent(""))]),
            new PromptMessage(PromptRole.User,
                [new PromptSection("Task", new TextContent("Do the thing."))]));

        var md = new MarkdownPromptRenderer().Render(prompt);

        Assert.DoesNotContain("## System", md, StringComparison.Ordinal);
        Assert.Contains("## User", md, StringComparison.Ordinal);
        Assert.Contains("Do the thing.", md, StringComparison.Ordinal);
    }

    [Fact]
    public void Chat_SkipsSectionsWithEmptyText()
    {
        var prompt = PromptWith(new PromptMessage(PromptRole.User,
        [
            new PromptSection("Filled", new TextContent("hello"), 0),
            new PromptSection("Empty",  new TextContent(" "), 1),
        ]));

        var msgs = new ChatMessageRenderer().Render(prompt);
        var msg = Assert.Single(msgs);

        // One block — content for the one filled section. Empty section dropped.
        // Section names are not emitted as content blocks.
        var only = Assert.Single(msg.Content);
        Assert.Equal("hello", Assert.IsType<ChatTextBlock>(only).Text);
    }

    [Fact]
    public void Chat_OmitsMessageEntirelyWhenAllSectionsAreEmpty()
    {
        var prompt = PromptWith(
            new PromptMessage(PromptRole.System,
                [new PromptSection("OptionalNotes", new TextContent("   "))]),
            new PromptMessage(PromptRole.User,
                [new PromptSection("Task", new TextContent("hi"))]));

        var msgs = new ChatMessageRenderer().Render(prompt);

        var only = Assert.Single(msgs);
        Assert.Equal("user", only.Role);
    }

    [Fact]
    public void NonTextContent_IsAlwaysRenderable_RegardlessOfFieldEmptiness()
    {
        // ImageContent / ToolCallContent / ToolResultContent never get suppressed by the
        // empty-text rule, even when their string fields are empty — they carry meaningful
        // structure beyond text and the consumer should decide what to do with them.
        var prompt = PromptWith(new PromptMessage(PromptRole.User,
        [
            new PromptSection("Img",  new ImageContent(Array.Empty<byte>(), "image/png"), 0),
            new PromptSection("Call", new ToolCallContent("id", "name", ""), 1),
        ]));

        var md = new MarkdownPromptRenderer().Render(prompt);
        Assert.Contains("### Img", md, StringComparison.Ordinal);
        Assert.Contains("### Call", md, StringComparison.Ordinal);

        var chat = new ChatMessageRenderer().Render(prompt);
        Assert.Single(chat);
        Assert.Equal(2, chat[0].Content.Count); // 2 content blocks; section names not emitted
    }
}

public class ChatMessageRendererTests
{
    [Fact]
    public void Render_EmitsOneBlockPerSection_NoHeaderInjection()
    {
        // Section names are metadata, not content. The chat renderer must not inject
        // "# Task" text blocks before each section's content — that would leak markdown
        // into the structured output that provider adapters forward to the model.
        var prompt = PromptBuilder.Create()
            .User(u => u.Section("Task", "Summarize."))
            .Build();

        var msgs = new ChatMessageRenderer().Render(prompt);
        var msg = Assert.Single(msgs);

        Assert.Equal("user", msg.Role);
        var only = Assert.Single(msg.Content);
        Assert.Equal("Summarize.", Assert.IsType<ChatTextBlock>(only).Text);
    }

    [Fact]
    public void Render_PreservesMultimodalBlocks()
    {
        var bytes = new byte[] { 9, 8, 7 };
        var prompt = PromptBuilder.Create()
            .User(u => u
                .Section("Q", "What is this?")
                .ImageSection("Photo", bytes, "image/jpeg"))
            .Assistant(a => a.ToolCallSection("Call", "c1", "lookup", "{}"))
            .Tool(t => t.ToolResultSection("Out", "c1", [new TextContent("done")]))
            .Build();

        var msgs = new ChatMessageRenderer().Render(prompt);
        Assert.Equal(3, msgs.Count);

        // user: 2 sections -> 2 content blocks (text + image), no section-name headers
        Assert.Equal("user", msgs[0].Role);
        Assert.Equal(2, msgs[0].Content.Count);
        Assert.Equal("What is this?", Assert.IsType<ChatTextBlock>(msgs[0].Content[0]).Text);
        var image = Assert.IsType<ChatImageBlock>(msgs[0].Content[1]);
        Assert.Equal("image/jpeg", image.MediaType);
        Assert.Equal(bytes, image.Data.ToArray());

        // assistant: 1 tool-call block
        var call = Assert.IsType<ChatToolCallBlock>(Assert.Single(msgs[1].Content));
        Assert.Equal("c1", call.ToolCallId);

        // tool: 1 tool-result block
        var result = Assert.IsType<ChatToolResultBlock>(Assert.Single(msgs[2].Content));
        Assert.Equal("c1", result.ToolCallId);
        Assert.Equal("done", Assert.IsType<ChatTextBlock>(result.Output[0]).Text);
    }
}
