# AI Prompt Builder Design Plan

## Overview

This design describes a .NET prompt-building library that supports two complementary creation paths:

1. **Code-defined prompts** — prompt text and templates are authored directly in C#.
2. **Repository-defined prompts** — prompt text and templates are loaded from files, databases, blob storage, or another external source.

Both paths produce the same internal model:

```text
PromptTemplate
PromptMessage
PromptSection
PromptVariable
PromptMetadata
BuiltPrompt
```

This keeps rendering, validation, hashing, provider adapters, and observability consistent regardless of where a prompt originates.

The design also includes **content hashes** as a first-class concept for traceability, caching, auditing, and debugging.

---

## Goals

- Support fluent manual prompt construction.
- Support reusable prompt templates.
- Support explicit prompt versioning.
- Support external prompt storage in files or databases.
- Preserve segregated messages instead of flattening everything into one string too early.
- Allow rendering to a single string only when needed.
- Support provider-specific adapters later.
- Track template hashes and built prompt hashes.
- Make prompt changes auditable and reproducible.

---

## High-Level Architecture

```text
                    ┌────────────────────┐
                    │  Manual C# Builder  │
                    └─────────┬──────────┘
                              │
                              ▼
┌────────────────────┐   ┌──────────────┐   ┌──────────────────┐
│ Code Templates     │──▶│ PromptBuilder│──▶│ BuiltPrompt       │
└────────────────────┘   └──────────────┘   └────────┬─────────┘
                                                      │
┌────────────────────┐   ┌──────────────┐             │
│ File/DB Repository │──▶│ PromptFactory│─────────────┘
└────────────────────┘   └──────────────┘
                                                      │
                                                      ▼
                                           ┌────────────────────┐
                                           │ Renderers/Adapters │
                                           └────────────────────┘
```

---

## Core Design Rule

```text
PromptBuilder builds prompts directly.
PromptTemplate represents reusable prompt definitions.
IPromptRepository loads reusable prompt definitions.
IPromptFactory builds prompts from repositories.
Extension methods improve ergonomics but should not be required.
Renderers convert BuiltPrompt into provider-specific formats.
Every built prompt has a content hash.
Every published repository prompt has a template hash.
```

---

## Domain Model

### PromptRole

```csharp
public enum PromptRole
{
    System,
    User,
    Assistant,
    Tool
}
```

---

### PromptKey

Identifies a prompt independently of version.

```csharp
public sealed record PromptKey(
    string Namespace,
    string Name
);
```

Example:

```csharp
new PromptKey("CodeReview", "SecurityReview")
```

---

### PromptVersion

Represents an explicit semantic-style prompt version.

```csharp
public sealed record PromptVersion(
    int Major,
    int Minor = 0,
    int Patch = 0)
{
    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public static PromptVersion V1 => new(1);
    public static PromptVersion V2 => new(2);
}
```

Prompt versioning should be stricter than normal package versioning because even small wording changes can affect AI output.

Recommended version rules:

```text
Major version changes:
- changed behavior
- changed output shape
- changed safety assumptions
- changed model-facing instructions in a meaningful way

Minor version changes:
- added optional sections
- improved wording without intended behavior change
- added metadata or variables

Patch version changes:
- typo fixes
- formatting fixes
- non-semantic cleanup
```

---

### VersionedPromptKey

Combines prompt identity and version.

```csharp
public sealed record VersionedPromptKey(
    PromptKey Key,
    PromptVersion Version
);
```

---

### PromptSection

A reusable unit of prompt content.

```csharp
public sealed record PromptSection(
    string Name,
    string Content,
    int Order = 0
);
```

---

### PromptMessage

A provider-neutral message containing one or more sections.

```csharp
public sealed record PromptMessage(
    PromptRole Role,
    IReadOnlyList<PromptSection> Sections
);
```

This keeps system, user, assistant, and tool messages separate until rendering.

---

### PromptVariable

Represents a variable required by a template.

```csharp
public sealed record PromptVariable(
    string Name,
    bool Required = true,
    string? Description = null,
    string? DefaultValue = null
);
```

---

### PromptMetadata

Metadata supports search, ownership, deprecation, and audit workflows.

```csharp
public sealed record PromptMetadata(
    string? Description = null,
    string? Owner = null,
    IReadOnlyList<string>? Tags = null,
    bool Deprecated = false,
    VersionedPromptKey? ReplacedBy = null
);
```

---

### PromptTemplate

A reusable prompt definition.

```csharp
public sealed record PromptTemplate(
    VersionedPromptKey Id,
    IReadOnlyList<PromptMessage> Messages,
    IReadOnlyList<PromptVariable> Variables,
    PromptMetadata Metadata,
    string? ContentHash = null
);
```

The `ContentHash` identifies the exact template content.

---

### BuiltPrompt

The final built prompt after direct construction or template rendering.

```csharp
public sealed record BuiltPrompt(
    VersionedPromptKey? Source,
    IReadOnlyList<PromptMessage> Messages,
    IReadOnlyDictionary<string, object?> Variables,
    string ContentHash
);
```

The `ContentHash` identifies the exact final prompt content that will be sent to a model.

---

## Two Creation Paths

## Path A: Manual Code-Defined Prompts

Manual construction is useful when prompts are small, stable, and tightly coupled to application logic.

```csharp
var prompt = PromptBuilder.Create()
    .System(system => system
        .Section("Role", "You are a senior .NET engineer.")
        .Section("Rules", "Do not invent missing context."))
    .User(user => user
        .Section("Task", "Review this code for maintainability.")
        .Section("Code", code)
        .Section("Output", "Return markdown with findings and recommendations."))
    .Build();
```

Best when:

```text
- the prompt is small
- the prompt is tightly coupled to code
- strong compile-time behavior is desirable
- no central editing workflow is needed
- fast local iteration matters
```

---

## Path B: Repository-Defined Prompts

Repository-defined prompts are useful when prompts are shared, centrally managed, frequently updated, or edited by non-developers.

```csharp
var prompt = await promptFactory.BuildFromTemplateAsync(
    key: new PromptKey("CodeReview", "SecurityReview"),
    version: new PromptVersion(2),
    variables: new
    {
        Language = "C#",
        Code = sourceCode
    });
```

Best when:

```text
- prompts are shared across projects
- prompts are updated without package releases
- central governance matters
- audit history matters
- an admin UI may exist later
```

---

## PromptBuilder

The builder supports direct construction and template usage.

```csharp
public sealed class PromptBuilder
{
    private readonly List<PromptMessage> _messages = [];
    private readonly Dictionary<string, object?> _variables = [];
    private readonly IPromptHasher _hasher;
    private VersionedPromptKey? _source;
    private string? _templateHash;

    private PromptBuilder(IPromptHasher hasher)
    {
        _hasher = hasher;
    }

    public static PromptBuilder Create(IPromptHasher? hasher = null)
    {
        return new PromptBuilder(hasher ?? new Sha256PromptHasher());
    }

    public PromptBuilder System(string content)
    {
        AddMessage(PromptRole.System, new[]
        {
            new PromptSection("System", content)
        });

        return this;
    }

    public PromptBuilder User(string content)
    {
        AddMessage(PromptRole.User, new[]
        {
            new PromptSection("User", content)
        });

        return this;
    }

    public PromptBuilder Assistant(string content)
    {
        AddMessage(PromptRole.Assistant, new[]
        {
            new PromptSection("Assistant", content)
        });

        return this;
    }

    public PromptBuilder System(Action<PromptMessageBuilder> configure)
    {
        return Message(PromptRole.System, configure);
    }

    public PromptBuilder User(Action<PromptMessageBuilder> configure)
    {
        return Message(PromptRole.User, configure);
    }

    public PromptBuilder Assistant(Action<PromptMessageBuilder> configure)
    {
        return Message(PromptRole.Assistant, configure);
    }

    public PromptBuilder Message(
        PromptRole role,
        Action<PromptMessageBuilder> configure)
    {
        var messageBuilder = new PromptMessageBuilder(role);
        configure(messageBuilder);

        _messages.Add(messageBuilder.Build());
        return this;
    }

    public PromptBuilder UseTemplate(
        PromptTemplate template,
        object? variables = null)
    {
        var variableMap = PromptVariableMapper.ToDictionary(variables);

        PromptTemplateValidator.Validate(template, variableMap);

        var renderedMessages = PromptTemplateRenderer.Render(
            template,
            variableMap);

        _messages.AddRange(renderedMessages);
        _variables.Merge(variableMap);
        _source = template.Id;
        _templateHash = _hasher.HashTemplate(template);

        return this;
    }

    public BuiltPrompt Build()
    {
        if (_messages.Count == 0)
            throw new InvalidOperationException("Prompt must contain at least one message.");

        var prompt = new BuiltPrompt(
            Source: _source,
            Messages: _messages.ToList(),
            Variables: _variables,
            ContentHash: string.Empty);

        return prompt with
        {
            ContentHash = _hasher.HashBuiltPrompt(prompt)
        };
    }

    private void AddMessage(PromptRole role, IReadOnlyList<PromptSection> sections)
    {
        if (sections.Count == 0)
            throw new ArgumentException("Message must contain at least one section.");

        _messages.Add(new PromptMessage(role, sections));
    }
}
```

---

## PromptMessageBuilder

```csharp
public sealed class PromptMessageBuilder
{
    private readonly PromptRole _role;
    private readonly List<PromptSection> _sections = [];
    private int _order;

    public PromptMessageBuilder(PromptRole role)
    {
        _role = role;
    }

    public PromptMessageBuilder Section(string name, string content)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Section name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Section content is required.", nameof(content));

        _sections.Add(new PromptSection(name.Trim(), content.Trim(), _order++));
        return this;
    }

    public PromptMessage Build()
    {
        if (_sections.Count == 0)
            throw new InvalidOperationException("Message must contain at least one section.");

        return new PromptMessage(
            _role,
            _sections.OrderBy(x => x.Order).ToList());
    }
}
```

---

## Code-Defined Templates

A prompt can be defined as a reusable C# template.

```csharp
public static class CodeReviewTemplates
{
    public static PromptTemplate SecurityReviewV1 =>
        new(
            Id: new VersionedPromptKey(
                new PromptKey("CodeReview", "SecurityReview"),
                new PromptVersion(1)),
            Messages: new[]
            {
                new PromptMessage(
                    PromptRole.System,
                    new[]
                    {
                        new PromptSection(
                            "Role",
                            "You are a senior application security engineer.")
                    }),
                new PromptMessage(
                    PromptRole.User,
                    new[]
                    {
                        new PromptSection(
                            "Task",
                            "Review the following {{Language}} code for security issues."),
                        new PromptSection(
                            "Code",
                            "{{Code}}"),
                        new PromptSection(
                            "Output",
                            "Return findings grouped by severity.")
                    })
            },
            Variables: new[]
            {
                new PromptVariable("Language"),
                new PromptVariable("Code")
            },
            Metadata: new PromptMetadata(
                Description: "Security-focused code review prompt.",
                Owner: "Platform")
        );
}
```

Usage:

```csharp
var prompt = PromptBuilder.Create()
    .UseTemplate(
        CodeReviewTemplates.SecurityReviewV1,
        new
        {
            Language = "C#",
            Code = sourceCode
        })
    .Build();
```

---

## Repository-Defined Templates

### Repository Interface

```csharp
public interface IPromptRepository
{
    Task<PromptTemplate?> GetTemplateAsync(
        PromptKey key,
        PromptVersion version,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PromptVersion>> GetVersionsAsync(
        PromptKey key,
        CancellationToken cancellationToken = default);
}
```

---

### File Repository

```csharp
public sealed class FilePromptRepository : IPromptRepository
{
    private readonly string _rootPath;

    public FilePromptRepository(string rootPath)
    {
        _rootPath = rootPath;
    }

    public async Task<PromptTemplate?> GetTemplateAsync(
        PromptKey key,
        PromptVersion version,
        CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(
            _rootPath,
            key.Namespace,
            key.Name,
            $"{version}.prompt.json");

        if (!File.Exists(path))
            return null;

        var json = await File.ReadAllTextAsync(path, cancellationToken);

        return PromptTemplateJsonSerializer.Deserialize(json);
    }

    public Task<IReadOnlyList<PromptVersion>> GetVersionsAsync(
        PromptKey key,
        CancellationToken cancellationToken = default)
    {
        // Scan directory and parse version filenames.
        throw new NotImplementedException();
    }
}
```

Example file layout:

```text
prompts/
  Common/
    TechnicalAssistant/
      1.0.0.prompt.json
      2.0.0.prompt.json

  CodeReview/
    SecurityReview/
      1.0.0.prompt.json
      2.0.0.prompt.json
```

---

### SQL Repository

```csharp
public sealed class SqlPromptRepository : IPromptRepository
{
    private readonly PromptDbContext _db;

    public SqlPromptRepository(PromptDbContext db)
    {
        _db = db;
    }

    public async Task<PromptTemplate?> GetTemplateAsync(
        PromptKey key,
        PromptVersion version,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.PromptTemplates
            .SingleOrDefaultAsync(x =>
                x.Namespace == key.Namespace &&
                x.Name == key.Name &&
                x.VersionMajor == version.Major &&
                x.VersionMinor == version.Minor &&
                x.VersionPatch == version.Patch &&
                x.Status == "Published",
                cancellationToken);

        return entity is null
            ? null
            : PromptTemplateJsonSerializer.Deserialize(entity.ContentJson);
    }
}
```

Suggested table:

```sql
CREATE TABLE PromptTemplates (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Namespace NVARCHAR(200) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    VersionMajor INT NOT NULL,
    VersionMinor INT NOT NULL,
    VersionPatch INT NOT NULL,
    Status NVARCHAR(50) NOT NULL,
    ContentJson NVARCHAR(MAX) NOT NULL,
    ContentHash NVARCHAR(128) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    CreatedBy NVARCHAR(200) NULL,
    DeprecatedAt DATETIMEOFFSET NULL,

    CONSTRAINT UQ_PromptTemplates_Version
        UNIQUE (Namespace, Name, VersionMajor, VersionMinor, VersionPatch),

    CONSTRAINT UQ_PromptTemplates_Hash
        UNIQUE (Namespace, Name, VersionMajor, VersionMinor, VersionPatch, ContentHash)
);
```

Recommended statuses:

```text
Draft
Published
Deprecated
Archived
```

---

## PromptFactory

Async fluent builders can become awkward. A factory is better for repository-defined prompts.

```csharp
public interface IPromptFactory
{
    Task<BuiltPrompt> BuildFromTemplateAsync(
        PromptKey key,
        PromptVersion version,
        object? variables = null,
        CancellationToken cancellationToken = default);

    PromptBuilder CreateBuilder();
}
```

Implementation:

```csharp
public sealed class PromptFactory : IPromptFactory
{
    private readonly IPromptRepository _repository;
    private readonly IPromptHasher _hasher;

    public PromptFactory(
        IPromptRepository repository,
        IPromptHasher hasher)
    {
        _repository = repository;
        _hasher = hasher;
    }

    public PromptBuilder CreateBuilder()
    {
        return PromptBuilder.Create(_hasher);
    }

    public async Task<BuiltPrompt> BuildFromTemplateAsync(
        PromptKey key,
        PromptVersion version,
        object? variables = null,
        CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetTemplateAsync(
            key,
            version,
            cancellationToken);

        if (template is null)
            throw new PromptNotFoundException(key, version);

        return PromptBuilder.Create(_hasher)
            .UseTemplate(template, variables)
            .Build();
    }
}
```

Usage:

```csharp
var manualPrompt = promptFactory.CreateBuilder()
    .System("You are a helpful assistant.")
    .User("Summarize this text.")
    .Build();
```

```csharp
var storedPrompt = await promptFactory.BuildFromTemplateAsync(
    new PromptKey("Support", "TicketClassifier"),
    new PromptVersion(1),
    new
    {
        TicketText = ticket.Body
    });
```

---

## Content Hashes

Content hashes are a first-class part of the design.

They answer two different questions:

```text
Template hash:
Which exact prompt template version was loaded?

Built prompt hash:
Which exact final rendered prompt was sent?
```

Both are useful because a template plus different variables can produce different final prompts.

---

## Hashing Interface

```csharp
public interface IPromptHasher
{
    string HashTemplate(PromptTemplate template);
    string HashBuiltPrompt(BuiltPrompt prompt);
}
```

---

## SHA-256 Implementation

```csharp
public sealed class Sha256PromptHasher : IPromptHasher
{
    public string HashTemplate(PromptTemplate template)
    {
        var canonical = PromptCanonicalizer.ForTemplate(template);
        return ComputeSha256(canonical);
    }

    public string HashBuiltPrompt(BuiltPrompt prompt)
    {
        var canonical = PromptCanonicalizer.ForBuiltPrompt(prompt);
        return ComputeSha256(canonical);
    }

    private static string ComputeSha256(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
```

---

## Canonicalization

Do not hash arbitrary JSON text directly. Formatting, property order, or whitespace could change the hash even when the prompt is semantically identical.

Instead, hash a deterministic canonical representation.

```csharp
public static class PromptCanonicalizer
{
    public static string ForTemplate(PromptTemplate template)
    {
        var canonical = new
        {
            key = new
            {
                ns = template.Id.Key.Namespace,
                name = template.Id.Key.Name,
                version = template.Id.Version.ToString()
            },
            messages = template.Messages.Select(m => new
            {
                role = m.Role.ToString(),
                sections = m.Sections
                    .OrderBy(s => s.Order)
                    .Select(s => new
                    {
                        name = s.Name,
                        content = Normalize(s.Content),
                        order = s.Order
                    })
            }),
            variables = template.Variables
                .OrderBy(v => v.Name)
                .Select(v => new
                {
                    name = v.Name,
                    required = v.Required,
                    defaultValue = v.DefaultValue
                })
        };

        return JsonSerializer.Serialize(canonical);
    }

    public static string ForBuiltPrompt(BuiltPrompt prompt)
    {
        var canonical = new
        {
            source = prompt.Source?.ToString(),
            messages = prompt.Messages.Select(m => new
            {
                role = m.Role.ToString(),
                sections = m.Sections
                    .OrderBy(s => s.Order)
                    .Select(s => new
                    {
                        name = s.Name,
                        content = Normalize(s.Content),
                        order = s.Order
                    })
            })
        };

        return JsonSerializer.Serialize(canonical);
    }

    private static string Normalize(string value)
    {
        return value
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Trim();
    }
}
```

---

## Hash Verification

Repository prompts should be verified when loaded.

```csharp
public sealed class VerifyingPromptRepository : IPromptRepository
{
    private readonly IPromptRepository _inner;
    private readonly IPromptHasher _hasher;

    public VerifyingPromptRepository(
        IPromptRepository inner,
        IPromptHasher hasher)
    {
        _inner = inner;
        _hasher = hasher;
    }

    public async Task<PromptTemplate?> GetTemplateAsync(
        PromptKey key,
        PromptVersion version,
        CancellationToken cancellationToken = default)
    {
        var template = await _inner.GetTemplateAsync(
            key,
            version,
            cancellationToken);

        if (template is null)
            return null;

        var computedHash = _hasher.HashTemplate(template);

        if (template.ContentHash is not null &&
            template.ContentHash != computedHash)
        {
            throw new PromptHashMismatchException(
                template.Id,
                expected: template.ContentHash,
                actual: computedHash);
        }

        return template with
        {
            ContentHash = computedHash
        };
    }

    public Task<IReadOnlyList<PromptVersion>> GetVersionsAsync(
        PromptKey key,
        CancellationToken cancellationToken = default)
    {
        return _inner.GetVersionsAsync(key, cancellationToken);
    }
}
```

This prevents silent drift if someone edits a stored prompt without publishing a new version.

---

## Hashing Invariants

```text
Every built prompt has a content hash.
Every published repository prompt has a template hash.
Exact prompt versions are immutable once published.
Changing prompt content should create a new version or fail hash verification.
Rendered prompts should log both template hash and built prompt hash when available.
```

---

## File Prompt Example

```json
{
  "id": {
    "key": {
      "namespace": "CodeReview",
      "name": "SecurityReview"
    },
    "version": {
      "major": 2,
      "minor": 0,
      "patch": 0
    }
  },
  "contentHash": "sha256:8c3f...",
  "messages": [
    {
      "role": "System",
      "sections": [
        {
          "name": "Role",
          "content": "You are a senior application security engineer.",
          "order": 0
        }
      ]
    },
    {
      "role": "User",
      "sections": [
        {
          "name": "Task",
          "content": "Review the following {{Language}} code for security issues.",
          "order": 0
        },
        {
          "name": "Code",
          "content": "{{Code}}",
          "order": 1
        },
        {
          "name": "Output",
          "content": "Return findings grouped by severity.",
          "order": 2
        }
      ]
    }
  ],
  "variables": [
    {
      "name": "Language",
      "required": true
    },
    {
      "name": "Code",
      "required": true
    }
  ],
  "metadata": {
    "description": "Security-focused code review prompt.",
    "owner": "Platform",
    "tags": [ "code-review", "security" ],
    "deprecated": false
  }
}
```

---

## Template Rendering

Keep template substitution small and explicit at first.

```csharp
public static class PromptTemplateRenderer
{
    public static IReadOnlyList<PromptMessage> Render(
        PromptTemplate template,
        IReadOnlyDictionary<string, object?> variables)
    {
        return template.Messages
            .Select(message => new PromptMessage(
                message.Role,
                message.Sections
                    .Select(section => section with
                    {
                        Content = ReplaceVariables(section.Content, variables)
                    })
                    .ToList()))
            .ToList();
    }

    private static string ReplaceVariables(
        string content,
        IReadOnlyDictionary<string, object?> variables)
    {
        foreach (var variable in variables)
        {
            content = content.Replace(
                "{{" + variable.Key + "}}",
                variable.Value?.ToString() ?? string.Empty,
                StringComparison.Ordinal);
        }

        return content;
    }
}
```

Later, this could be replaced with a template engine such as Scriban or Liquid if loops, conditionals, or filters are needed.

---

## Template Validation

```csharp
public static class PromptTemplateValidator
{
    public static void Validate(
        PromptTemplate template,
        IReadOnlyDictionary<string, object?> variables)
    {
        foreach (var variable in template.Variables.Where(x => x.Required))
        {
            if (!variables.ContainsKey(variable.Name))
            {
                throw new PromptValidationException(
                    $"Missing required variable: {variable.Name}");
            }
        }
    }
}
```

Stored prompts should also be validated before publishing:

```text
- valid JSON/YAML schema
- valid role values
- unique section ordering
- declared variables match placeholders
- no missing required metadata
- version is immutable once published
- content hash matches canonical content
```

---

## Rendering

Rendering is separate from building.

```csharp
public interface IPromptRenderer<TOutput>
{
    TOutput Render(BuiltPrompt prompt);
}
```

---

## Markdown/String Renderer

```csharp
public sealed class MarkdownPromptRenderer : IPromptRenderer<string>
{
    public string Render(BuiltPrompt prompt)
    {
        var builder = new StringBuilder();

        foreach (var message in prompt.Messages)
        {
            builder.AppendLine($"## {message.Role}");

            foreach (var section in message.Sections.OrderBy(x => x.Order))
            {
                builder.AppendLine();
                builder.AppendLine($"### {section.Name}");
                builder.AppendLine(section.Content);
            }

            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }
}
```

---

## Chat Message Renderer

```csharp
public sealed record ChatPromptMessage(
    string Role,
    string Content
);
```

```csharp
public sealed class ChatMessageRenderer
    : IPromptRenderer<IReadOnlyList<ChatPromptMessage>>
{
    public IReadOnlyList<ChatPromptMessage> Render(BuiltPrompt prompt)
    {
        return prompt.Messages
            .Select(message =>
            {
                var content = string.Join(
                    "\n\n",
                    message.Sections
                        .OrderBy(x => x.Order)
                        .Select(x => $"# {x.Name}\n{x.Content}"));

                return new ChatPromptMessage(
                    Role: message.Role.ToString().ToLowerInvariant(),
                    Content: content);
            })
            .ToList();
    }
}
```

---

## Extension Methods

Extension methods should be convenience APIs, not the only way to use prompts.

### Code-Defined Extension Method

```csharp
public static class CodeDefinedPromptExtensions
{
    public static PromptBuilder UseTechnicalAssistantV1(
        this PromptBuilder builder)
    {
        return builder.System(system => system
            .Section("Role", "You are a senior technical assistant.")
            .Section("Rules", "Be precise and explicit about uncertainty."));
    }
}
```

Usage:

```csharp
var prompt = PromptBuilder.Create()
    .UseTechnicalAssistantV1()
    .User(user => user
        .Section("Task", "Explain this error.")
        .Section("Error", error))
    .Build();
```

---

### Repository-Defined Extension Method

```csharp
public static class RepositoryPromptExtensions
{
    public static Task<BuiltPrompt> BuildSecurityCodeReviewAsync(
        this IPromptFactory factory,
        PromptVersion version,
        string language,
        string code,
        CancellationToken cancellationToken = default)
    {
        return factory.BuildFromTemplateAsync(
            PromptKeys.CodeReview.SecurityReview,
            version,
            new
            {
                Language = language,
                Code = code
            },
            cancellationToken);
    }
}
```

Usage:

```csharp
var prompt = await promptFactory.BuildSecurityCodeReviewAsync(
    version: new PromptVersion(2),
    language: "C#",
    code: sourceCode);
```

---

## Prompt Keys

Common prompt identifiers can be defined in code even when content lives externally.

```csharp
public static class PromptKeys
{
    public static class CodeReview
    {
        public static readonly PromptKey SecurityReview =
            new("CodeReview", "SecurityReview");

        public static readonly PromptKey MaintainabilityReview =
            new("CodeReview", "MaintainabilityReview");
    }

    public static class Support
    {
        public static readonly PromptKey TicketClassifier =
            new("Support", "TicketClassifier");
    }

    public static class Common
    {
        public static readonly PromptKey TechnicalAssistant =
            new("Common", "TechnicalAssistant");
    }
}
```

This gives users discoverability while still allowing the prompt body to live in code, files, or a database.

---

## Composite Repository

A composite repository allows multiple prompt sources.

```csharp
public sealed class CompositePromptRepository : IPromptRepository
{
    private readonly IReadOnlyList<IPromptRepository> _repositories;

    public CompositePromptRepository(IEnumerable<IPromptRepository> repositories)
    {
        _repositories = repositories.ToList();
    }

    public async Task<PromptTemplate?> GetTemplateAsync(
        PromptKey key,
        PromptVersion version,
        CancellationToken cancellationToken = default)
    {
        foreach (var repository in _repositories)
        {
            var prompt = await repository.GetTemplateAsync(
                key,
                version,
                cancellationToken);

            if (prompt is not null)
                return prompt;
        }

        return null;
    }

    public async Task<IReadOnlyList<PromptVersion>> GetVersionsAsync(
        PromptKey key,
        CancellationToken cancellationToken = default)
    {
        var versions = new List<PromptVersion>();

        foreach (var repository in _repositories)
        {
            versions.AddRange(await repository.GetVersionsAsync(key, cancellationToken));
        }

        return versions
            .Distinct()
            .OrderByDescending(x => x.Major)
            .ThenByDescending(x => x.Minor)
            .ThenByDescending(x => x.Patch)
            .ToList();
    }
}
```

---

## Dependency Injection

Example configuration:

```csharp
services.AddPrompting()
    .AddCodeDefinedPrompts()
    .AddFilePromptRepository("prompts")
    .AddSqlPromptRepository(connectionString)
    .AddPromptCaching();
```

Possible extension methods:

```csharp
public static class PromptingServiceCollectionExtensions
{
    public static IServiceCollection AddPrompting(
        this IServiceCollection services)
    {
        services.AddSingleton<IPromptHasher, Sha256PromptHasher>();
        services.AddSingleton<IPromptFactory, PromptFactory>();
        services.AddSingleton<IPromptRenderer<string>, MarkdownPromptRenderer>();
        services.AddSingleton<IPromptRenderer<IReadOnlyList<ChatPromptMessage>>, ChatMessageRenderer>();

        return services;
    }

    public static IServiceCollection AddFilePromptRepository(
        this IServiceCollection services,
        string rootPath)
    {
        services.AddSingleton<IPromptRepository>(sp =>
        {
            var hasher = sp.GetRequiredService<IPromptHasher>();
            var fileRepository = new FilePromptRepository(rootPath);
            return new VerifyingPromptRepository(fileRepository, hasher);
        });

        return services;
    }
}
```

---

## Logging and Observability

Every AI call should be able to log:

```text
PromptKey: CodeReview.SecurityReview
PromptVersion: 2.0.0
TemplateHash: sha256:8c3f...
BuiltPromptHash: sha256:91fa...
Renderer: ChatMessageRenderer
Model: gpt-x
```

This lets you answer:

```text
Which template version was used?
Was the stored prompt modified?
Which exact rendered prompt did the model receive?
Did two outputs come from the same prompt content?
```

---

## Package Structure

Recommended packages:

```text
AiPrompting.Core
  Domain models
  PromptBuilder
  PromptMessageBuilder
  PromptTemplate rendering
  Validation interfaces
  Repository interfaces
  Hashing interfaces

AiPrompting.Rendering
  Markdown renderer
  Plain text renderer
  Chat message renderer

AiPrompting.Files
  FilePromptRepository
  JSON/YAML serialization
  File schema validation

AiPrompting.Sql
  SqlPromptRepository
  EF Core model
  Optional migrations

AiPrompting.Caching
  CachingPromptRepository

AiPrompting.CommonPrompts
  Common code-defined prompts
  Prompt keys
  Extension methods

AiPrompting.OpenAI
  Adapters from BuiltPrompt to OpenAI-style chat messages

AiPrompting.Anthropic
  Adapters from BuiltPrompt to Anthropic-style messages
```

---

## Tradeoffs

## Code-Defined Prompt Path

Advantages:

```text
- simplest path
- strongly typed
- easy to debug
- no runtime storage dependency
- good for stable prompts
- works well with extension methods
```

Disadvantages:

```text
- prompt changes require code/package deployment
- harder for non-developers to edit
- less suitable for central prompt governance
```

---

## Repository-Defined Prompt Path

Advantages:

```text
- prompt changes do not require package updates
- supports central registry
- supports admin UI later
- good for cross-project reuse
- easier to audit and publish versions
```

Disadvantages:

```text
- less compile-time safety
- more runtime failure modes
- needs caching
- needs schema validation
- needs access control if database-backed
```

---

## Combined Approach

Advantages:

```text
- supports simple and advanced users
- one internal model
- one rendering system
- one validation system
- one hashing system
- migration path from code-defined to external prompts
```

Disadvantages:

```text
- larger surface area
- more documentation required
- careful naming needed to avoid confusion
```

---

## Recommended API Surface

### Manual Path

```csharp
var prompt = PromptBuilder.Create()
    .System("You are a helpful assistant.")
    .User(user => user
        .Section("Task", "Summarize the text.")
        .Section("Text", input))
    .Build();
```

---

### Code-Defined Template Path

```csharp
var prompt = PromptBuilder.Create()
    .UseTemplate(
        CodeReviewTemplates.SecurityReviewV1,
        new
        {
            Language = "C#",
            Code = sourceCode
        })
    .Build();
```

---

### Repository-Defined Template Path

```csharp
var prompt = await promptFactory.BuildFromTemplateAsync(
    PromptKeys.CodeReview.SecurityReview,
    new PromptVersion(2),
    new
    {
        Language = "C#",
        Code = sourceCode
    });
```

---

### Code-Defined Extension Method Path

```csharp
var prompt = PromptBuilder.Create()
    .UseTechnicalAssistantV1()
    .User(user => user
        .Section("Task", "Explain this error.")
        .Section("Error", error))
    .Build();
```

---

### Repository Extension Method Path

```csharp
var prompt = await promptFactory.BuildSecurityCodeReviewAsync(
    version: new PromptVersion(2),
    language: "C#",
    code: sourceCode);
```

---

## Implementation Order

```text
1. Core domain models
2. Manual PromptBuilder
3. PromptMessageBuilder
4. BuiltPrompt
5. IPromptHasher
6. SHA-256 hasher
7. Canonicalizer
8. Markdown/string renderer
9. Chat message renderer
10. Code-defined PromptTemplate support
11. Variable substitution
12. Template validation
13. Template hash validation
14. IPromptRepository
15. FilePromptRepository
16. PromptFactory
17. Extension methods
18. Caching repository
19. SQL/database repository
20. Audit/logging helpers
21. Provider-specific adapters
```

---

## Final Recommendation

Build the library as one coherent prompt system with two clear creation paths:

```text
1. Direct/manual construction with PromptBuilder
2. Versioned template loading with IPromptRepository and PromptFactory
```

Keep these principles stable:

```text
- Messages stay segregated until rendering.
- Prompt templates are reusable and versioned.
- Prompt content can live in code, files, or a database.
- Extension methods improve ergonomics but are optional.
- Exact versions are preferred for production workflows.
- Repository prompts should be immutable once published.
- Every built prompt has a content hash.
- Every published repository prompt has a template hash.
```

This gives the library a simple entry point for small use cases and a maintainable path for larger systems that need reuse, governance, versioning, and auditability.
