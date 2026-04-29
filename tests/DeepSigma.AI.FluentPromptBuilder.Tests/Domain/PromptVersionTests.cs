using DeepSigma.AI.FluentPromptBuilder.Domain;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Tests.Domain;

public class PromptVersionTests
{
    [Theory]
    [InlineData("1", 1, 0, 0)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("0.0.0", 0, 0, 0)]
    public void TryParse_ValidInput_ReturnsExpectedComponents(string input, int major, int minor, int patch)
    {
        Assert.True(PromptVersion.TryParse(input, out var version));
        Assert.Equal(new PromptVersion(major, minor, patch), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1.2.3.4")]
    [InlineData("a.b.c")]
    [InlineData("-1.0.0")]
    [InlineData("1.-2.0")]
    [InlineData("1..2")]
    [InlineData(".")]
    public void TryParse_InvalidInput_ReturnsFalse(string? input)
    {
        Assert.False(PromptVersion.TryParse(input, out _));
    }

    [Fact]
    public void Parse_InvalidInput_Throws()
    {
        Assert.Throws<FormatException>(() => PromptVersion.Parse("nope"));
    }

    [Fact]
    public void ToString_ProducesDottedTriple()
    {
        Assert.Equal("1.2.3", new PromptVersion(1, 2, 3).ToString());
        Assert.Equal("4.0.0", new PromptVersion(4).ToString());
    }

    [Fact]
    public void ToString_ParseRoundTrip()
    {
        var original = new PromptVersion(7, 8, 9);
        Assert.Equal(original, PromptVersion.Parse(original.ToString()));
    }

    [Theory]
    [InlineData(1, 0, 0, 2, 0, 0, -1)]
    [InlineData(2, 0, 0, 1, 0, 0, +1)]
    [InlineData(1, 2, 0, 1, 3, 0, -1)]
    [InlineData(1, 2, 5, 1, 2, 6, -1)]
    [InlineData(1, 2, 3, 1, 2, 3,  0)]
    public void CompareTo_OrdersMajorMinorPatch(int aMa, int aMi, int aPa, int bMa, int bMi, int bPa, int sign)
    {
        var a = new PromptVersion(aMa, aMi, aPa);
        var b = new PromptVersion(bMa, bMi, bPa);
        Assert.Equal(sign, Math.Sign(a.CompareTo(b)));
    }

    [Fact]
    public void ComparisonOperators_FollowCompareTo()
    {
        var lower = new PromptVersion(1, 2, 3);
        var same = new PromptVersion(1, 2, 3);
        var higher = new PromptVersion(1, 3, 0);

        Assert.True(lower < higher);
        Assert.True(lower <= higher);
        Assert.True(higher > lower);
        Assert.True(higher >= lower);
        Assert.True(lower <= same);
        Assert.True(lower >= same);
        Assert.False(lower > higher);
        Assert.False(lower >= higher);
    }

    [Fact]
    public void Equality_StructuralOnComponents()
    {
        Assert.Equal(new PromptVersion(2, 1, 0), new PromptVersion(2, 1, 0));
        Assert.NotEqual(new PromptVersion(2, 1, 0), new PromptVersion(2, 1, 1));
    }
}
