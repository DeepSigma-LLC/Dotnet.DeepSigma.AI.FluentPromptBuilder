using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;
using DeepSigma.AI.FluentPromptBuilder.Serialization;

namespace DeepSigma.AI.FluentPromptBuilder.Repositories;

/// <summary>
/// A file-system-backed <see cref="IPromptRepository"/> that loads prompt templates from a
/// directory tree shaped <c>{root}/{namespace}/{name}/{version}.prompt.json</c>.
/// </summary>
/// <remarks>
/// <para>Path-traversal hardening: <see cref="PromptKey"/> already disallows path-component
/// characters and whitespace, but this implementation additionally canonicalizes the resolved
/// file path and asserts that it is contained within the configured root.</para>
/// <para>Files must include <c>"$schemaVersion": 1</c>; unknown versions are rejected with
/// <see cref="PromptSerializationException"/>.</para>
/// </remarks>
public sealed class FilePromptRepository : IPromptRepository
{
    private const string FileExtension = ".prompt.json";

    private readonly string _rootPath;

    /// <summary>Constructs a repository rooted at the given directory.</summary>
    /// <param name="rootPath">The directory under which prompt files live.</param>
    public FilePromptRepository(string rootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        _rootPath = Path.GetFullPath(rootPath);
    }

    /// <summary>The fully-qualified root path used by this repository.</summary>
    public string RootPath => _rootPath;

    /// <inheritdoc/>
    public async Task<PromptTemplate?> GetTemplateAsync(
        PromptKey key,
        PromptVersion version,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var path = ResolveFilePath(key, version);
        if (!File.Exists(path))
        {
            return null;
        }

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            throw new PromptSerializationException($"Failed to read prompt file: {path}.", ex);
        }

        return PromptTemplateJsonSerializer.Deserialize(json);
    }

    /// <inheritdoc/>
    public async Task<PromptTemplate?> GetLatestAsync(
        PromptKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var versions = await GetVersionsAsync(key, cancellationToken).ConfigureAwait(false);
        if (versions.Count == 0)
        {
            return null;
        }

        var latest = versions[^1];
        return await GetTemplateAsync(key, latest, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<PromptVersion>> GetVersionsAsync(
        PromptKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = ResolveDirectory(key);
        if (!Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<PromptVersion>>([]);
        }

        var versions = new List<PromptVersion>();
        foreach (var file in Directory.EnumerateFiles(directory, "*" + FileExtension))
        {
            var fileName = Path.GetFileName(file);
            // Strip ".prompt.json" suffix.
            var versionPart = fileName[..^FileExtension.Length];
            if (PromptVersion.TryParse(versionPart, out var version))
            {
                versions.Add(version);
            }
        }

        versions.Sort();
        return Task.FromResult<IReadOnlyList<PromptVersion>>(versions);
    }

    private string ResolveDirectory(PromptKey key)
    {
        var combined = Path.Combine(_rootPath, key.Namespace, key.Name);
        var canonical = Path.GetFullPath(combined);
        AssertWithinRoot(canonical);
        return canonical;
    }

    private string ResolveFilePath(PromptKey key, PromptVersion version)
    {
        var directory = ResolveDirectory(key);
        var filePath = Path.Combine(directory, version + FileExtension);
        var canonical = Path.GetFullPath(filePath);
        AssertWithinRoot(canonical);
        return canonical;
    }

    private void AssertWithinRoot(string canonicalPath)
    {
        var rootWithSep = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;

        if (!canonicalPath.StartsWith(rootWithSep, StringComparison.Ordinal) &&
            !canonicalPath.Equals(_rootPath, StringComparison.Ordinal))
        {
            throw new PromptValidationException(
                $"Resolved path '{canonicalPath}' escapes repository root '{_rootPath}'.");
        }
    }
}
