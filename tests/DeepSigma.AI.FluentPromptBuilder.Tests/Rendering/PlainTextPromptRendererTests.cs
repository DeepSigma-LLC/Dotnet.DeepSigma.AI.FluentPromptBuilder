using DeepSigma.AI.FluentPromptBuilder.Building;
using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Rendering;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Tests.Rendering;

public class PlainTextPromptRendererTests
{
    private static BuiltPrompt SampleTextPrompt() =>
        PromptBuilder.Create()
            .System("You are helpful.")
            .User(u => u
                .Section("Task", "Summarize the error.")
                .Section("Error", "NullReferenceException at MyService.Process(line 42)."))
            .Build();

    [Fact]
    public void Default_IsContentOnly()
    {
        Assert.Equal(PlainTextStyle.ContentOnly, new PlainTextPromptRenderer().Style);
    }

    [Fact]
    public void ContentOnly_OmitsRoleAndSectionLabels()
    {
        var text = new PlainTextPromptRenderer(PlainTextStyle.ContentOnly).Render(SampleTextPrompt());

        Assert.Contains("You are helpful.", text, StringComparison.Ordinal);
        Assert.Contains("Summarize the error.", text, StringComparison.Ordinal);
        Assert.Contains("NullReferenceException at MyService", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[System]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("[User]", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Task:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Error:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("##", text, StringComparison.Ordinal);
        Assert.DoesNotContain("###", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Transcript_IncludesRoleHeadersButNoSectionNames()
    {
        var text = new PlainTextPromptRenderer(PlainTextStyle.Transcript).Render(SampleTextPrompt());

        Assert.Contains("[System]", text, StringComparison.Ordinal);
        Assert.Contains("[User]", text, StringComparison.Ordinal);
        Assert.Contains("Summarize the error.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Task:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("##", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Labeled_IncludesRoleAndSectionNames()
    {
        var text = new PlainTextPromptRenderer(PlainTextStyle.Labeled).Render(SampleTextPrompt());

        // Lines normalised to \n so the test is platform-agnostic.
        var normalised = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("System\n", normalised, StringComparison.Ordinal);
        Assert.Contains("User\n", normalised, StringComparison.Ordinal);
        Assert.Contains("  Task:", normalised, StringComparison.Ordinal);
        Assert.Contains("  Error:", normalised, StringComparison.Ordinal);
        Assert.DoesNotContain("##", normalised, StringComparison.Ordinal);
    }

    [Fact]
    public void Image_RendersAsPlaceholderInAllStyles()
    {
        var prompt = PromptBuilder.Create()
            .User(u => u.ImageSection("Photo", new byte[] { 1, 2, 3 }, "image/png"))
            .Build();

        foreach (var style in Enum.GetValues<PlainTextStyle>())
        {
            var text = new PlainTextPromptRenderer(style).Render(prompt);
            Assert.Contains("[image: image/png, 3 bytes]", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void ToolCall_RendersAsCompactText()
    {
        var prompt = PromptBuilder.Create()
            .Assistant(a => a.ToolCallSection("Call", "c1", "lookup", "{\"id\":42}"))
            .Build();

        var text = new PlainTextPromptRenderer().Render(prompt);
        Assert.Contains("[tool_call lookup(c1): {\"id\":42}]", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ToolResult_IncludesNestedContentAndErrorMarker()
    {
        var prompt = PromptBuilder.Create()
            .Tool(t => t.ToolResultSection("Out", "c1",
                output: [new TextContent("found"), new TextContent("done")],
                isError: true))
            .Build();

        var text = new PlainTextPromptRenderer().Render(prompt);
        Assert.Contains("[tool_result c1 (error)]", text, StringComparison.Ordinal);
        Assert.Contains("found", text, StringComparison.Ordinal);
        Assert.Contains("done", text, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptySections_AreSuppressed()
    {
        var prompt = PromptBuilder.Create()
            .System(s => s.Section("Real", "yes").Section("Empty", "  "))
            .User("real user")
            .Build();

        var text = new PlainTextPromptRenderer(PlainTextStyle.Labeled).Render(prompt);
        Assert.Contains("  Real:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("  Empty:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void AllEmptySections_OmitMessageEntirely()
    {
        // Using direct PromptSection construction since the builder rejects empty messages.
        var prompt = new BuiltPrompt(
            Source: null,
            Messages:
            [
                new PromptMessage(PromptRole.System,
                    [new PromptSection("Empty", new TextContent("   "))]),
                new PromptMessage(PromptRole.User,
                    [new PromptSection("Real", new TextContent("hi"))]),
            ]);

        var text = new PlainTextPromptRenderer(PlainTextStyle.Transcript).Render(prompt);
        Assert.DoesNotContain("[System]", text, StringComparison.Ordinal);
        Assert.Contains("[User]", text, StringComparison.Ordinal);
    }
}
