using DeepSigma.AI.FluentPromptBuilder.Domain;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Tests.Domain;

public class PromptRoleExtensionsTests
{
    [Theory]
    [InlineData(PromptRole.System, "system")]
    [InlineData(PromptRole.User, "user")]
    [InlineData(PromptRole.Assistant, "assistant")]
    [InlineData(PromptRole.Tool, "tool")]
    public void ToApiString_MapsKnownRolesToLowercase(PromptRole role, string expected)
    {
        Assert.Equal(expected, role.ToApiString());
    }
}
