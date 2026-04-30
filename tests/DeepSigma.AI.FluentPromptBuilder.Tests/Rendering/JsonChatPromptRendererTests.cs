using System.Text.Json;
using DeepSigma.AI.FluentPromptBuilder.Building;
using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Rendering;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Tests.Rendering;

public class JsonChatPromptRendererTests
{
    [Fact]
    public void Render_TextOnly_ProducesValidLowercaseRoleJson()
    {
        var prompt = PromptBuilder.Create()
            .System("You are helpful.")
            .User("Hi.")
            .Build();

        var json = new JsonChatPromptRenderer().Render(prompt);

        using var doc = JsonDocument.Parse(json);
        var arr = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, arr.Count);
        Assert.Equal("system", arr[0].GetProperty("role").GetString());
        Assert.Equal("user", arr[1].GetProperty("role").GetString());

        var firstContent = arr[0].GetProperty("content").EnumerateArray().Single();
        Assert.Equal("text", firstContent.GetProperty("type").GetString());
        Assert.Equal("You are helpful.", firstContent.GetProperty("text").GetString());
    }

    [Fact]
    public void Render_OmitsSectionNames()
    {
        var prompt = PromptBuilder.Create()
            .User(u => u.Section("Question", "What's up?"))
            .Build();

        var json = new JsonChatPromptRenderer().Render(prompt);

        // Section name "Question" must not leak into content.
        Assert.DoesNotContain("Question", json, StringComparison.Ordinal);
        Assert.DoesNotContain("# ", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_Multimodal_AllVariantsAppearWithCorrectDiscriminators()
    {
        var prompt = PromptBuilder.Create()
            .User(u => u
                .Section("Q", "What's in this image?")
                .ImageSection("Photo", new byte[] { 1, 2, 3 }, "image/png"))
            .Assistant(a => a.ToolCallSection("Call", "c1", "lookup", "{\"id\":42}"))
            .Tool(t => t.ToolResultSection("Out", "c1", [new TextContent("found")]))
            .Build();

        var json = new JsonChatPromptRenderer(indented: false).Render(prompt);
        using var doc = JsonDocument.Parse(json);
        var msgs = doc.RootElement.EnumerateArray().ToList();
        Assert.Equal(3, msgs.Count);

        var userBlocks = msgs[0].GetProperty("content").EnumerateArray().ToList();
        Assert.Equal("text", userBlocks[0].GetProperty("type").GetString());
        Assert.Equal("image", userBlocks[1].GetProperty("type").GetString());
        Assert.Equal("image/png", userBlocks[1].GetProperty("mediaType").GetString());

        var call = msgs[1].GetProperty("content").EnumerateArray().Single();
        Assert.Equal("tool_call", call.GetProperty("type").GetString());
        Assert.Equal("c1", call.GetProperty("toolCallId").GetString());

        var result = msgs[2].GetProperty("content").EnumerateArray().Single();
        Assert.Equal("tool_result", result.GetProperty("type").GetString());
    }

    [Fact]
    public void Render_EmptySectionsAndMessages_AreSuppressed()
    {
        var prompt = PromptBuilder.Create()
            .System(s => s.Section("Empty", "   "))
            .User("real")
            .Build();

        var json = new JsonChatPromptRenderer().Render(prompt);

        using var doc = JsonDocument.Parse(json);
        var msg = doc.RootElement.EnumerateArray().Single();
        Assert.Equal("user", msg.GetProperty("role").GetString());
    }

    [Fact]
    public void Render_IndentationFlagControlsOutput()
    {
        var prompt = PromptBuilder.Create().System("hi").Build();

        var indented = new JsonChatPromptRenderer(indented: true).Render(prompt);
        var compact = new JsonChatPromptRenderer(indented: false).Render(prompt);

        Assert.Contains('\n', indented);
        Assert.DoesNotContain('\n', compact);
    }
}
