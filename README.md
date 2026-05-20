# DeepSigma.AI.FluentPromptBuilder

A lightweight, extensible fluent prompt-building library for .NET 10. Build prompts in code,
load them from files (or any storage backend you wire up), substitute variables, and render
to markdown or structured chat messages — all from one provider-neutral domain model.

> **Status:** v0.x — public API may change between minor versions while it settles.

---

## Why

Most prompt libraries either flatten everything to a single string too early, or grow into
full prompt-management platforms. This library aims for the middle: a small, immutable domain
model with one fluent entry point and clean extension seams.

Three creation paths share the same internal representation:

1. **Manual** — author prompts directly in C#.
2. **Code-defined templates** — reusable `PromptTemplate` instances declared in code.
3. **Repository-defined templates** — load from disk (or your own backend) via `IPromptRepository`.

---

## Install

```xml
<PackageReference Include="DeepSigma.AI.FluentPromptBuilder" Version="0.1.0" />
```

Targets `net10.0`. Tests use `xunit.v3`.

---

## Quick start

### 1. Manual fluent build

```csharp
using DeepSigma.AI.FluentPromptBuilder.Building;
using DeepSigma.AI.FluentPromptBuilder.Rendering;

var prompt = PromptBuilder.Create()
    .System("You are a helpful technical assistant.")
    .User(u => u
        .Section("Task", "Summarize the following error.")
        .Section("Error", error.Message))
    .Build();

var markdown = new MarkdownPromptRenderer().Render(prompt);
```

### 2. Multimodal content

`PromptContent` is a sealed hierarchy. Text is the common case; images, tool calls, and tool
results are first-class.

```csharp
var prompt = PromptBuilder.Create()
    .User(u => u
        .Section("Question", "What's in this image?")
        .ImageSection("Photo", File.ReadAllBytes("cat.png"), "image/png"))
    .Assistant(a => a
        .ToolCallSection("Call",
            toolCallId: "call_1",
            toolName: "lookup",
            argumentsJson: """{"id":42}"""))
    .Tool(t => t.ToolResultSection(
        "Result",
        toolCallId: "call_1",
        output: [new TextContent("found")]))
    .Build();

var chatMessages = new ChatMessageRenderer().Render(prompt);
// IReadOnlyList<ChatPromptMessage> with structured ChatContentBlocks —
// feed straight into a provider adapter without re-parsing strings.
```

There are also two JSON renderers:

```csharp
// Provider-neutral chat-message JSON — flat shape suitable for forwarding to an LLM.
// Drops section names, drops empty sections, lowercase roles, tagged content discriminator.
string chatJson = new JsonChatPromptRenderer().Render(prompt);

// Round-trip-friendly JSON in the same v1 schema as stored templates — preserves
// section names, ordering, and Source. Useful for caching, audit logs, transport.
string roundTripJson = BuiltPromptJsonSerializer.Serialize(prompt);
var rebuilt = BuiltPromptJsonSerializer.Deserialize(roundTripJson);
```

…and a plain-text renderer with three layout styles:

```csharp
// A — content only (default). Just the text, blank-line separated. No labels.
//     Best for legacy single-string completion APIs.
string text = new PlainTextPromptRenderer().Render(prompt);

// B — transcript style. [Role] headers, sections concatenated underneath.
//     Best for human-readable logs.
string transcript = new PlainTextPromptRenderer(PlainTextStyle.Transcript).Render(prompt);

// C — labeled. Role line + indented "  Section:" labels for each section.
//     Best when section names carry meaning consumers should read.
string labelled = new PlainTextPromptRenderer(PlainTextStyle.Labeled).Render(prompt);
```

Plain text can't embed images, so they render as `[image: image/png, N bytes]`. Tool calls
render as `[tool_call name(id): args]` and tool results as a labelled block with their nested
content recursively rendered.

### 3. Templates with variable substitution

```csharp
var template = new PromptTemplate(
    new VersionedPromptKey(
        new PromptKey("CodeReview", "SecurityReview"),
        new PromptVersion(1)),
    [
        new PromptMessage(PromptRole.System,
            [new PromptSection("Role", new TextContent("You are a senior security engineer."))]),
        new PromptMessage(PromptRole.User,
            [new PromptSection("Task", new TextContent("Review {{Language}} code: {{Code}}"))]),
    ],
    [new PromptVariable("Language"), new PromptVariable("Code")],
    new PromptMetadata());

var prompt = PromptBuilder.Create()
    .UseTemplate(template, new { Language = "C#", Code = sourceCode })
    .Build();
```

Substitution uses `{{Name}}` placeholders. The escape sequence `{{{{Name}}}}` renders as a
literal `{{Name}}`. Required variables are validated *before* rendering — missing variables
throw `PromptValidationException`.

### 4. File-loaded templates via DI

```csharp
using Microsoft.Extensions.DependencyInjection;
using DeepSigma.AI.FluentPromptBuilder.DependencyInjection;
using DeepSigma.AI.FluentPromptBuilder.Repositories;

var services = new ServiceCollection()
    .AddFluentPromptBuilder()
    .AddFilePromptRepository("./prompts")
    .BuildServiceProvider();

var factory = services.GetRequiredService<IPromptFactory>();
var prompt = await factory.BuildLatestAsync(
    new PromptKey("CodeReview", "SecurityReview"),
    new { Language = "C#", Code = sourceCode });
```

File layout is `{root}/{namespace}/{name}/{version}.prompt.json`. See
`samples/DeepSigma.AI.FluentPromptBuilder.Sample/prompts/...` for an example.

### 5. Postgres-backed templates

Install the optional companion package:

```xml
<PackageReference Include="DeepSigma.AI.FluentPromptBuilder.Postgres" Version="0.1.0" />
```

Wire it up the same way as the file repository:

```csharp
using DeepSigma.AI.FluentPromptBuilder.Postgres;

var services = new ServiceCollection()
    .AddFluentPromptBuilder()
    .AddPostgresPromptRepository("Host=localhost;Database=prompts;Username=app;Password=...")
    .BuildServiceProvider();

var factory = services.GetRequiredService<IPromptFactory>();
var prompt = await factory.BuildLatestAsync(
    new PromptKey("CodeReview", "SecurityReview"),
    new { Language = "C#", Code = sourceCode });
```

Templates are stored as `jsonb` in the same v1 wire format used by the file repository.
The schema uses a surrogate UUID primary key, a status lookup table with foreign-key
reference, and audit columns for `created_by` / `deprecated_at`:

```sql
CREATE TABLE IF NOT EXISTS prompt_template_statuses (
    status_id    smallint  PRIMARY KEY,
    status_name  text      NOT NULL UNIQUE
);

INSERT INTO prompt_template_statuses (status_id, status_name) VALUES
    (1, 'Draft'),
    (2, 'Published'),
    (3, 'Deprecated'),
    (4, 'Archived')
ON CONFLICT (status_id) DO NOTHING;

CREATE TABLE IF NOT EXISTS prompt_templates (
    id             uuid         PRIMARY KEY,
    namespace      text         NOT NULL,
    name           text         NOT NULL,
    version_major  int          NOT NULL,
    version_minor  int          NOT NULL,
    version_patch  int          NOT NULL,
    status_id      smallint     NOT NULL REFERENCES prompt_template_statuses(status_id),
    content_json   jsonb        NOT NULL,
    created_at     timestamptz  NOT NULL DEFAULT now(),
    created_by     text         NULL,
    deprecated_at  timestamptz  NULL,
    UNIQUE (namespace, name, version_major, version_minor, version_patch)
);

CREATE INDEX IF NOT EXISTS idx_prompt_templates_key_lookup
    ON prompt_templates (namespace, name, status_id,
                         version_major DESC, version_minor DESC, version_patch DESC);
```

Apply via your migration tool of choice (Flyway, dbup, EF migrations, manual SQL). Or
call `PostgresSchema.CreateSchemaSql()` to obtain the DDL programmatically. For local/dev
scenarios there's also `PostgresPromptSchemaInitializer.EnsureCreatedAsync(connectionString)`
which runs the idempotent DDL for you.

#### UUIDv7 expected for `id`

The `id` column has no SQL `DEFAULT`. Callers supply a **UUIDv7** (time-ordered) value when
inserting — UUIDv7 sorts by creation time, which gives much better insert and index
performance than v4. In .NET 9+:

```csharp
var id = Guid.CreateVersion7();
```

Postgres 17+ also exposes a native `uuidv7()` function if you prefer a SQL-side default.

#### Status filtering

`GetLatestAsync(key)` returns the latest **Published** version. To query by another status:

```csharp
var draft = await repository.GetLatestAsync(key, PromptStatus.Draft);
```

`GetTemplateAsync(key, version)` returns the row regardless of status — explicit ask,
explicit answer.

The repository is **read-only in v1** — populate the table from your own seed script,
migration, or admin UI. Write methods (`Upsert`, `Delete`) are on the roadmap.

Under the hood the repository runs on
[`DeepSigma.DataAccess.Postgres`](https://www.nuget.org/packages/DeepSigma.DataAccess.Postgres),
so `AddPostgresPromptRepository(connectionString)` also registers `RelationalDatabaseApi`,
`IDbConnectionFactory`, and the schema / bulk-copy services from that package — handy if
your app already uses them.

---

## What each renderer produces

Captured from the sample console; copy-paste reproducible by running
`dotnet run --project samples/DeepSigma.AI.FluentPromptBuilder.Sample`.

### Source — text-only manual prompt

```csharp
var prompt = PromptBuilder.Create()
    .System("You are a helpful technical assistant.")
    .User(u => u
        .Section("Task", "Summarize the following error.")
        .Section("Error", "NullReferenceException at MyService.Process(line 42)."))
    .Build();
```

**`MarkdownPromptRenderer`**

```text
## System

### System
You are a helpful technical assistant.

## User

### Task
Summarize the following error.

### Error
NullReferenceException at MyService.Process(line 42).
```

**`JsonChatPromptRenderer`**

```json
[
  {
    "role": "system",
    "content": [
      { "type": "text", "text": "You are a helpful technical assistant." }
    ]
  },
  {
    "role": "user",
    "content": [
      { "type": "text", "text": "Summarize the following error." },
      { "type": "text", "text": "NullReferenceException at MyService.Process(line 42)." }
    ]
  }
]
```

**`PlainTextPromptRenderer(PlainTextStyle.ContentOnly)`** — the default

```text
You are a helpful technical assistant.

Summarize the following error.

NullReferenceException at MyService.Process(line 42).
```

**`PlainTextPromptRenderer(PlainTextStyle.Transcript)`**

```text
[System]
You are a helpful technical assistant.

[User]
Summarize the following error.

NullReferenceException at MyService.Process(line 42).
```

**`PlainTextPromptRenderer(PlainTextStyle.Labeled)`**

```text
System
  System:
    You are a helpful technical assistant.

User
  Task:
    Summarize the following error.
  Error:
    NullReferenceException at MyService.Process(line 42).
```

---

### Source — multimodal prompt (image + tool-call + tool-result)

```csharp
var prompt = PromptBuilder.Create()
    .User(u => u
        .Section("Question", "What's in this image?")
        .ImageSection("Photo", new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "image/png"))
    .Assistant(a => a
        .Section("Reply", "Let me look that up.")
        .ToolCallSection("Call",
            toolCallId: "call_1",
            toolName: "lookup",
            argumentsJson: """{"q":"png-header"}"""))
    .Tool(t => t.ToolResultSection(
        "Result", toolCallId: "call_1",
        output: [new TextContent("PNG file signature.")]))
    .Build();
```

**`ChatMessageRenderer`** — structured `IReadOnlyList<ChatMessage>` (printed via the sample's `Describe` helper):

```text
[user] (2 blocks)
  text: What's in this image?
  image: image/png, 4 bytes
[assistant] (2 blocks)
  text: Let me look that up.
  tool_call: lookup (id=call_1) args={"q":"png-header"}
[tool] (1 block)
  tool_result: id=call_1, isError=False, 1 nested block(s)
```

**`JsonChatPromptRenderer`** — image bytes base64-encoded, tool-call/result use the same tagged-discriminator schema as stored templates:

```json
[
  {
    "role": "user",
    "content": [
      { "type": "text", "text": "What's in this image?" },
      { "type": "image", "mediaType": "image/png", "data": "iVBORw==" }
    ]
  },
  {
    "role": "assistant",
    "content": [
      { "type": "text", "text": "Let me look that up." },
      {
        "type": "tool_call",
        "toolCallId": "call_1",
        "toolName": "lookup",
        "argumentsJson": "{\"q\":\"png-header\"}"
      }
    ]
  },
  {
    "role": "tool",
    "content": [
      {
        "type": "tool_result",
        "toolCallId": "call_1",
        "isError": false,
        "output": [
          { "type": "text", "text": "PNG file signature." }
        ]
      }
    ]
  }
]
```

**`PlainTextPromptRenderer(PlainTextStyle.Labeled)`** — images become a placeholder line, tool calls/results become bracketed text:

```text
User
  Question:
    What's in this image?
  Photo:
    [image: image/png, 4 bytes]

Assistant
  Reply:
    Let me look that up.
  Call:
    [tool_call lookup(call_1): {"q":"png-header"}]

Tool
  Result:
    [tool_result call_1]
    PNG file signature.
```

---

## File schema (v1)

```json
{
  "$schemaVersion": 1,
  "id": {
    "key": { "namespace": "CodeReview", "name": "SecurityReview" },
    "version": { "major": 1, "minor": 0, "patch": 0 }
  },
  "messages": [
    {
      "role": "User",
      "sections": [
        { "name": "Task", "order": 0,
          "content": { "type": "text", "text": "Review {{Language}} code." } },
        { "name": "Code", "order": 1,
          "content": { "type": "text", "text": "{{Code}}" } }
      ]
    }
  ],
  "variables": [
    { "name": "Language", "required": true },
    { "name": "Code",     "required": true }
  ],
  "metadata": {
    "description": "Security-focused code review prompt.",
    "owner": "Platform",
    "tags": [ "code-review", "security" ]
  }
}
```

Content polymorphism uses a `type` discriminator so future variants are additive without a
schema bump:

| `type`        | Fields                                                      |
|---------------|-------------------------------------------------------------|
| `text`        | `text`                                                      |
| `image`       | `mediaType`, `data` (base64)                                |
| `tool_call`   | `toolCallId`, `toolName`, `argumentsJson`                   |
| `tool_result` | `toolCallId`, `isError`, `output` (nested content array)    |

Files containing an unrecognized `$schemaVersion` are rejected with
`PromptSerializationException`.

---

## Versioning prompts

`PromptVersion` is `Major.Minor.Patch` — implement `IComparable<PromptVersion>` and the full
set of comparison operators, parses with `Parse`/`TryParse`. Recommended bump rules (stricter
than typical package semver because wording is behavior):

| Bump      | When                                                                       |
|-----------|----------------------------------------------------------------------------|
| **Major** | Behavior change, output-shape change, safety-assumption change             |
| **Minor** | Added optional sections / variables, wording polish without intent changes |
| **Patch** | Typos, formatting, non-semantic cleanup                                    |

Treat published versions as immutable.

---

## Extension points

| Interface                 | Purpose                                                  |
|---------------------------|----------------------------------------------------------|
| `IPromptRepository`       | Load templates by key/version (file, DB, Redis, ...)     |
| `ITemplateRenderer`       | Variable substitution engine (default: regex-based)      |
| `IPromptRenderer<T>`      | Render `BuiltPrompt` to a target shape                   |
| `IPromptFactory`          | Compose repository + renderer; default impl provided     |

Swap any of these for your own implementation; nothing in the core needs to change.

---

## Roadmap

The following capabilities are intentionally **not** in v1 and will land later as additive
changes or separate packages, only as real consumer needs emerge.

### Content hashing (planned)

Prompt audit / traceability via deterministic content hashes. Sketch:

- Add `string ContentHash` to `BuiltPrompt` and an optional `string? ContentHash` to
  `PromptTemplate`. Both additive — no breaking change.
- Define an `IPromptCanonical.WriteCanonical(Utf8JsonWriter)` interface implemented by each
  domain type. Each type writes its own deterministic, alphabetically-keyed JSON shape — no
  central canonicalizer to keep in sync, easy to extend with new content types.
- `Sha256PromptHasher` runs the canonical writer into an `ArrayBufferWriter<byte>`, hashes
  with `SHA256.HashData`, formats as `sha256:<hex>`.
- File schema gains an optional `contentHash` field; `FilePromptRepository` verifies it on
  load if present and throws on drift.
- Pinned by golden-file tests so any change to the canonical form is an intentional breaking
  change.

### Database-backed repositories

Postgres is shipped as a companion package — see [Section 5](#5-postgres-backed-templates).
Other backends (SQL Server, MySQL, Redis, DynamoDB) can be added in separate packages by
implementing `IPromptRepository`; nothing in the core library needs to change.

Postgres write API (`Upsert`, `Delete`) — planned, not in v1.

### Provider adapters (planned)

`DeepSigma.AI.FluentPromptBuilder.OpenAI` and
`DeepSigma.AI.FluentPromptBuilder.Anthropic` packages will provide `IPromptRenderer<T>`
implementations that produce provider-native chat message shapes from `ChatPromptMessage`s.

### Caching

Not planned. The library does no I/O of its own; caching belongs at the consumer's storage
layer.

---

## License

See [LICENSE](LICENSE).
