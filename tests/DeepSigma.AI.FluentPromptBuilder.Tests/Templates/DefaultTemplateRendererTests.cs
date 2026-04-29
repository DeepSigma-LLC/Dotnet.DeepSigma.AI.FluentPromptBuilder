using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Templates;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Tests.Templates;

public class DefaultTemplateRendererTests
{
    private static readonly DefaultTemplateRenderer Renderer = new();

    private static PromptTemplate Template(params PromptMessage[] messages) =>
        new(
            new VersionedPromptKey(new PromptKey("T", "T"), new PromptVersion(1)),
            messages,
            [],
            new PromptMetadata());

    private static IReadOnlyDictionary<string, object?> Vars(params (string, object?)[] pairs) =>
        pairs.ToDictionary(p => p.Item1, p => p.Item2, StringComparer.Ordinal);

    [Fact]
    public void Substitute_ReplacesNamedPlaceholders()
    {
        var s = DefaultTemplateRenderer.Substitute("Hello, {{Name}}!", Vars(("Name", "world")));
        Assert.Equal("Hello, world!", s);
    }

    [Fact]
    public void Substitute_MissingVariable_LeavesPlaceholderUntouched()
    {
        var s = DefaultTemplateRenderer.Substitute("Hi {{Missing}}", Vars());
        Assert.Equal("Hi {{Missing}}", s);
    }

    [Fact]
    public void Substitute_DoesNotReSubstituteValues()
    {
        // The regression bug from the original plan: a value containing {{Other}} should NOT
        // trigger another substitution pass.
        var s = DefaultTemplateRenderer.Substitute(
            "{{A}} {{B}}",
            Vars(("A", "{{B}}"), ("B", "should-not-leak")));
        Assert.Equal("{{B}} should-not-leak", s);
    }

    [Fact]
    public void Substitute_RespectsEscape()
    {
        var s = DefaultTemplateRenderer.Substitute("{{{{Foo}}}} and {{Bar}}", Vars(("Bar", "x")));
        Assert.Equal("{{Foo}} and x", s);
    }

    [Fact]
    public void Substitute_ToleratesWhitespaceInsidePlaceholder()
    {
        var s = DefaultTemplateRenderer.Substitute("{{ Name }}", Vars(("Name", "ok")));
        Assert.Equal("ok", s);
    }

    [Fact]
    public void Substitute_NullValue_RendersAsEmpty()
    {
        var s = DefaultTemplateRenderer.Substitute("[{{X}}]", Vars(("X", (object?)null)));
        Assert.Equal("[]", s);
    }

    [Fact]
    public void Render_RecursesIntoToolCallArguments()
    {
        var template = Template(
            new PromptMessage(PromptRole.Assistant,
                [new PromptSection("Call",
                    new ToolCallContent("call_1", "lookup", "{\"q\":\"{{Query}}\"}"))]));

        var rendered = Renderer.Render(template, Vars(("Query", "frodo")));
        var call = Assert.IsType<ToolCallContent>(rendered[0].Sections[0].Content);
        Assert.Equal("{\"q\":\"frodo\"}", call.ArgumentsJson);
    }

    [Fact]
    public void Render_RecursesIntoToolResultOutput()
    {
        var template = Template(
            new PromptMessage(PromptRole.Tool,
                [new PromptSection("Result",
                    new ToolResultContent("call_1",
                        [new TextContent("Hi {{Name}}"), new TextContent("done")]))]));

        var rendered = Renderer.Render(template, Vars(("Name", "world")));
        var result = Assert.IsType<ToolResultContent>(rendered[0].Sections[0].Content);
        Assert.Equal("Hi world", Assert.IsType<TextContent>(result.Output[0]).Text);
        Assert.Equal("done", Assert.IsType<TextContent>(result.Output[1]).Text);
    }

    [Fact]
    public void Render_LeavesImageContentUntouched()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var template = Template(
            new PromptMessage(PromptRole.User,
                [new PromptSection("Photo", new ImageContent(bytes, "image/png"))]));

        var rendered = Renderer.Render(template, Vars(("X", "ignored")));
        var image = Assert.IsType<ImageContent>(rendered[0].Sections[0].Content);
        Assert.Equal("image/png", image.MediaType);
        Assert.Equal(bytes, image.Data.ToArray());
    }
}
