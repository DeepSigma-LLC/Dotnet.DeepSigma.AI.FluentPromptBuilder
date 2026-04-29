using System.Globalization;
using System.Numerics;

namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// A semantic-style version for a prompt template. Major / Minor / Patch are non-negative integers.
/// Wording-as-behavior: even small wording changes can affect AI output, so prompt versioning is
/// stricter than typical package semver. See README for the recommended bump rules.
/// </summary>
public readonly record struct PromptVersion(int Major, int Minor = 0, int Patch = 0)
    : IComparable<PromptVersion>, IComparisonOperators<PromptVersion, PromptVersion, bool>
{
    /// <summary>Renders as <c>Major.Minor.Patch</c>.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

    /// <summary>Parses a <c>"M"</c>, <c>"M.m"</c>, or <c>"M.m.p"</c> version string.</summary>
    /// <exception cref="FormatException">If the input is not a valid version.</exception>
    public static PromptVersion Parse(string s) =>
        TryParse(s, out var v) ? v : throw new FormatException($"Invalid prompt version: '{s}'.");

    /// <summary>Tries to parse a <c>"M"</c>, <c>"M.m"</c>, or <c>"M.m.p"</c> version string.</summary>
    public static bool TryParse(string? s, out PromptVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        var parts = s.Split('.');
        if (parts.Length is < 1 or > 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var major) || major < 0)
        {
            return false;
        }

        var minor = 0;
        var patch = 0;

        if (parts.Length > 1 && (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out minor) || minor < 0))
        {
            return false;
        }

        if (parts.Length > 2 && (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out patch) || patch < 0))
        {
            return false;
        }

        version = new PromptVersion(major, minor, patch);
        return true;
    }

    /// <inheritdoc/>
    public int CompareTo(PromptVersion other)
    {
        if (Major != other.Major)
        {
            return Major.CompareTo(other.Major);
        }

        if (Minor != other.Minor)
        {
            return Minor.CompareTo(other.Minor);
        }

        return Patch.CompareTo(other.Patch);
    }

    /// <summary>Returns whether <paramref name="left"/> precedes <paramref name="right"/>.</summary>
    public static bool operator <(PromptVersion left, PromptVersion right) => left.CompareTo(right) < 0;

    /// <summary>Returns whether <paramref name="left"/> precedes or equals <paramref name="right"/>.</summary>
    public static bool operator <=(PromptVersion left, PromptVersion right) => left.CompareTo(right) <= 0;

    /// <summary>Returns whether <paramref name="left"/> follows <paramref name="right"/>.</summary>
    public static bool operator >(PromptVersion left, PromptVersion right) => left.CompareTo(right) > 0;

    /// <summary>Returns whether <paramref name="left"/> follows or equals <paramref name="right"/>.</summary>
    public static bool operator >=(PromptVersion left, PromptVersion right) => left.CompareTo(right) >= 0;
}
