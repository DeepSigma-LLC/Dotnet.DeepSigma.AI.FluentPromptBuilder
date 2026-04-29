using DeepSigma.AI.FluentPromptBuilder.Building;
using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Tests.Building;

public class PromptBuilderTests
{
    [Fact]
    public void Build_EmptyBuilder_Throws()
    {
        Assert.Throws<PromptValidationException>(() => PromptBuilder.Create().Build());
    }

    [Fact]
    public void System_User_Assistant_StringOverloads_AppendOneSectionEach()
    {
        var prompt = PromptBuilder.Create()
            .System("You are helpful.")
            .User("Hi.")
            .Assistant("Hello!")
            .Build();

        Assert.Equal(3, prompt.Messages.Count);
        Assert.Equal(PromptRole.System, prompt.Messages[0].Role);
        Assert.Equal(PromptRole.User, prompt.Messages[1].Role);
        Assert.Equal(PromptRole.Assistant, prompt.Messages[2].Role);

        var first = Assert.Single(prompt.Messages[0].Sections);
        Assert.Equal("System", first.Name);
        Assert.Equal("You are helpful.", Assert.IsType<TextContent>(first.Content).Text);
    }

    [Fact]
    public void Configure_Action_AppendsMultipleSectionsInOrder()
    {
        var prompt = PromptBuilder.Create()
            .User(u => u
                .Section("Task", "Review code.")
                .Section("Code", "var x = 1;")
                .Section("Output", "Markdown."))
            .Build();

        var message = Assert.Single(prompt.Messages);
        Assert.Collection(
            message.Sections,
            s => { Assert.Equal("Task", s.Name); Assert.Equal(0, s.Order); },
            s => { Assert.Equal("Code", s.Name); Assert.Equal(1, s.Order); },
            s => { Assert.Equal("Output", s.Name); Assert.Equal(2, s.Order); });
    }

    [Fact]
    public void MessageBuilder_RejectsEmptyMessage()
    {
        Assert.Throws<PromptValidationException>(() =>
            PromptBuilder.Create().System(_ => { }).Build());
    }

    [Fact]
    public void ImageSection_PreservesBytesAndMediaType()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var prompt = PromptBuilder.Create()
            .User(u => u
                .Section("Q", "What is this?")
                .ImageSection("Photo", bytes, "image/png"))
            .Build();

        var imageSection = prompt.Messages[0].Sections[1];
        var image = Assert.IsType<ImageContent>(imageSection.Content);
        Assert.Equal("image/png", image.MediaType);
        Assert.Equal(bytes, image.Data.ToArray());
    }

    [Fact]
    public void ToolCallSection_CarriesCallIdAndArguments()
    {
        var prompt = PromptBuilder.Create()
            .Assistant(a => a.ToolCallSection("Call", "call_1", "lookup", "{\"id\":42}"))
            .Build();

        var call = Assert.IsType<ToolCallContent>(prompt.Messages[0].Sections[0].Content);
        Assert.Equal("call_1", call.ToolCallId);
        Assert.Equal("lookup", call.ToolName);
        Assert.Equal("{\"id\":42}", call.ArgumentsJson);
    }

    [Fact]
    public void ToolResultSection_CarriesNestedOutput()
    {
        var prompt = PromptBuilder.Create()
            .Tool(t => t.ToolResultSection("Result", "call_1",
                [new TextContent("42 found"), new TextContent("done")]))
            .Build();

        var result = Assert.IsType<ToolResultContent>(prompt.Messages[0].Sections[0].Content);
        Assert.Equal("call_1", result.ToolCallId);
        Assert.Equal(2, result.Output.Count);
        Assert.False(result.IsError);
    }

    [Fact]
    public void UseTemplate_SubstitutesVariablesAndRecordsSource()
    {
        var template = new PromptTemplate(
            new VersionedPromptKey(new PromptKey("Test", "Greeting"), new PromptVersion(1)),
            [
                new PromptMessage(PromptRole.System, [new PromptSection("Role", new TextContent("You are {{Persona}}."))]),
                new PromptMessage(PromptRole.User, [new PromptSection("Greet", new TextContent("Hello, {{Name}}!"))]),
            ],
            [new PromptVariable("Persona"), new PromptVariable("Name")],
            new PromptMetadata());

        var prompt = PromptBuilder.Create()
            .UseTemplate(template, new { Persona = "helpful", Name = "world" })
            .Build();

        Assert.Equal(template.Id, prompt.Source);
        Assert.Equal("You are helpful.", Assert.IsType<TextContent>(prompt.Messages[0].Sections[0].Content).Text);
        Assert.Equal("Hello, world!", Assert.IsType<TextContent>(prompt.Messages[1].Sections[0].Content).Text);
    }

    [Fact]
    public void UseTemplate_MissingRequiredVariable_Throws()
    {
        var template = new PromptTemplate(
            new VersionedPromptKey(new PromptKey("Test", "Greeting"), new PromptVersion(1)),
            [new PromptMessage(PromptRole.User, [new PromptSection("Greet", new TextContent("Hi {{Name}}"))])],
            [new PromptVariable("Name", Required: true)],
            new PromptMetadata());

        Assert.Throws<PromptValidationException>(() =>
            PromptBuilder.Create().UseTemplate(template).Build());
    }

    [Fact]
    public void UseTemplate_OptionalVariableWithDefault_UsesDefault()
    {
        var template = new PromptTemplate(
            new VersionedPromptKey(new PromptKey("Test", "Greeting"), new PromptVersion(1)),
            [new PromptMessage(PromptRole.User, [new PromptSection("Greet", new TextContent("Hi {{Name}}"))])],
            [new PromptVariable("Name", Required: false, DefaultValue: "world")],
            new PromptMetadata());

        var prompt = PromptBuilder.Create().UseTemplate(template).Build();
        Assert.Equal("Hi world", Assert.IsType<TextContent>(prompt.Messages[0].Sections[0].Content).Text);
    }
}
