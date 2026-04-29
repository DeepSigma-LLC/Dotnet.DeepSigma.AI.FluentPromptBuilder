using DeepSigma.AI.FluentPromptBuilder.Domain;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Tests.Domain;

public class PromptKeyTests
{
    [Fact]
    public void Constructor_AcceptsValidComponents()
    {
        var key = new PromptKey("CodeReview", "SecurityReview");
        Assert.Equal("CodeReview", key.Namespace);
        Assert.Equal("SecurityReview", key.Name);
    }

    [Fact]
    public void ToString_RendersSlashSeparated()
    {
        Assert.Equal("CodeReview/SecurityReview",
            new PromptKey("CodeReview", "SecurityReview").ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrWhitespace_Throws(string? input)
    {
        Assert.Throws<ArgumentException>(() => new PromptKey(input!, "Name"));
        Assert.Throws<ArgumentException>(() => new PromptKey("Namespace", input!));
    }

    [Theory]
    [InlineData("Bad/Namespace")]
    [InlineData("Bad\\Namespace")]
    [InlineData("Bad.Namespace")]
    [InlineData("Bad:Namespace")]
    [InlineData("Bad Namespace")]
    [InlineData("..")]
    [InlineData("../etc")]
    public void Constructor_DisallowedCharacters_Throws(string input)
    {
        Assert.Throws<ArgumentException>(() => new PromptKey(input, "Name"));
        Assert.Throws<ArgumentException>(() => new PromptKey("Namespace", input));
    }

    [Fact]
    public void Equality_StructuralOnComponents()
    {
        var a = new PromptKey("Common", "Greeting");
        var b = new PromptKey("Common", "Greeting");
        var c = new PromptKey("Common", "Different");

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}

public class VersionedPromptKeyTests
{
    [Fact]
    public void ToString_FormatsKeyAtVersion()
    {
        var versioned = new VersionedPromptKey(
            new PromptKey("CodeReview", "SecurityReview"),
            new PromptVersion(2, 1, 0));

        Assert.Equal("CodeReview/SecurityReview@2.1.0", versioned.ToString());
    }

    [Fact]
    public void Equality_StructuralOnComponents()
    {
        var a = new VersionedPromptKey(new PromptKey("A", "B"), new PromptVersion(1));
        var b = new VersionedPromptKey(new PromptKey("A", "B"), new PromptVersion(1));
        var c = new VersionedPromptKey(new PromptKey("A", "B"), new PromptVersion(2));

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
