using System.Text.Json;
using Hx.Cli.Kernel;
using Hx.Tooling.Contracts;
using Xunit;

namespace Hx.Cli.Kernel.Tests;

/// <summary>
/// Proves every migrated command's envelope validates against the published JSON Schema artifact
/// (<c>schemas/cli-envelope.schema.json</c>). A focused, dependency-free schema validator
/// (type / enum / required / additionalProperties / $ref / items) reads the real schema file and checks
/// representative success, failure, skipped, and blocked envelopes from each ring.
/// </summary>
public sealed class EnvelopeSchemaConformanceTests
{
    private static readonly CliMeta Meta = new("hx-test", "1.0.0");
    private static readonly JsonSerializerOptions Options = JsonContractSerializerOptions.Create();

    public static TheoryData<string, CliResult> Envelopes() => new()
    {
        { "ok+data+next", CliResults.Ok(Meta, "plan", "ok", new { count = 1 }, nextActions: [new CliNextAction("run", "why", "cmd")]) },
        { "fail+diag+data", CliResults.Fail(Meta, "check", ExitClass.Validation, [Diag.Of(ErrorCodes.Validation_Failed, "bad", target: "f.cs")], "failed", new { y = 2 }) },
        { "skipped", CliResults.Skipped(Meta, "security scan", "advisory in dev") },
        { "blocked+operator", CliResults.Blocked(Meta, "sample blocked command", ExitClass.Validation, [Diag.Of(ErrorCodes.Validation_Failed, "stale proof")], "refused", nextActions: [new CliNextAction("fix", "why")]) },
        { "ok+effects", CliResults.Ok(Meta, "hygiene gitleaks render-config", "rendered", new { path = "x" }, effects: [new CliEffect("write", "tools/gitleaks/config/gitleaks.toml", "120 chars")]) },
    };

    [Theory]
    [MemberData(nameof(Envelopes))]
    public void Envelope_validates_against_the_published_schema(string label, CliResult result)
    {
        using JsonDocument schemaDoc = JsonDocument.Parse(File.ReadAllText(SchemaPath()));
        using JsonDocument instance = JsonDocument.Parse(JsonSerializer.Serialize(result, Options));

        List<string> violations = [.. Validate(instance.RootElement, schemaDoc.RootElement, schemaDoc.RootElement, "$")];
        Assert.True(violations.Count == 0, $"[{label}] schema violations: {string.Join("; ", violations)}");
    }

    [Fact]
    public void The_published_schema_is_well_formed_and_closed()
    {
        using JsonDocument schemaDoc = JsonDocument.Parse(File.ReadAllText(SchemaPath()));
        JsonElement root = schemaDoc.RootElement;
        Assert.Equal("object", root.GetProperty("type").GetString());
        Assert.False(root.GetProperty("additionalProperties").GetBoolean()); // closed envelope
        Assert.Contains(root.GetProperty("required").EnumerateArray(), e => e.GetString() == "schemaVersion");
    }

    // ---- a focused JSON-Schema subset validator (enough for the envelope contract) ----

    private static IEnumerable<string> Validate(JsonElement value, JsonElement schema, JsonElement root, string path)
    {
        JsonElement resolvedSchema = ResolveRef(schema, root);
        foreach (string violation in ValidateType(value, resolvedSchema, path)) { yield return violation; }
        foreach (string violation in ValidateEnum(value, resolvedSchema, path)) { yield return violation; }
        foreach (string violation in ValidateObject(value, resolvedSchema, root, path)) { yield return violation; }
        foreach (string violation in ValidateArray(value, resolvedSchema, root, path)) { yield return violation; }
    }

    private static JsonElement ResolveRef(JsonElement schema, JsonElement root) =>
        schema.TryGetProperty("$ref", out JsonElement refEl) ? Resolve(root, refEl.GetString()!) : schema;

    private static IEnumerable<string> ValidateType(JsonElement value, JsonElement schema, string path)
    {
        if (schema.TryGetProperty("type", out JsonElement typeEl))
        {
            string[] allowed = typeEl.ValueKind == JsonValueKind.Array
                ? [.. typeEl.EnumerateArray().Select(t => t.GetString()!)]
                : [typeEl.GetString()!];
            if (!TypeMatches(value, allowed))
            {
                yield return $"{path}: expected type [{string.Join("|", allowed)}] but got {value.ValueKind}";
            }
        }
    }

    private static IEnumerable<string> ValidateEnum(JsonElement value, JsonElement schema, string path)
    {
        if (schema.TryGetProperty("enum", out JsonElement enumEl)
            && !enumEl.EnumerateArray().Any(e => JsonEquals(e, value)))
        {
            yield return $"{path}: value '{value}' not permitted by enum";
        }
    }

    private static IEnumerable<string> ValidateObject(JsonElement value, JsonElement schema, JsonElement root, string path)
    {
        if (value.ValueKind == JsonValueKind.Object && schema.TryGetProperty("properties", out JsonElement props))
        {
            foreach (string violation in ValidateRequired(value, schema, path))
            {
                yield return violation;
            }

            bool closed = schema.TryGetProperty("additionalProperties", out JsonElement ap) && ap.ValueKind == JsonValueKind.False;
            foreach (JsonProperty member in value.EnumerateObject())
            {
                if (props.TryGetProperty(member.Name, out JsonElement memberSchema))
                {
                    foreach (string v in Validate(member.Value, memberSchema, root, $"{path}.{member.Name}"))
                    {
                        yield return v;
                    }
                }
                else if (closed)
                {
                    yield return $"{path}: unexpected property '{member.Name}'";
                }
            }
        }
    }

    private static IEnumerable<string> ValidateRequired(JsonElement value, JsonElement schema, string path)
    {
        if (!schema.TryGetProperty("required", out JsonElement required))
        {
            yield break;
        }

        foreach (JsonElement r in required.EnumerateArray())
        {
            if (!value.TryGetProperty(r.GetString()!, out _))
            {
                yield return $"{path}: missing required property '{r.GetString()}'";
            }
        }
    }

    private static IEnumerable<string> ValidateArray(JsonElement value, JsonElement schema, JsonElement root, string path)
    {
        if (value.ValueKind == JsonValueKind.Array && schema.TryGetProperty("items", out JsonElement items))
        {
            int i = 0;
            foreach (JsonElement element in value.EnumerateArray())
            {
                foreach (string v in Validate(element, items, root, $"{path}[{i}]"))
                {
                    yield return v;
                }

                i++;
            }
        }
    }

    private static JsonElement Resolve(JsonElement root, string pointer)
    {
        JsonElement current = root;
        foreach (string segment in pointer.TrimStart('#', '/').Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current.GetProperty(segment);
        }

        return current;
    }

    private static bool TypeMatches(JsonElement value, string[] allowed) => value.ValueKind switch
    {
        JsonValueKind.Object => allowed.Contains("object"),
        JsonValueKind.Array => allowed.Contains("array"),
        JsonValueKind.String => allowed.Contains("string"),
        JsonValueKind.Number => allowed.Contains("integer") || allowed.Contains("number"),
        JsonValueKind.True or JsonValueKind.False => allowed.Contains("boolean"),
        JsonValueKind.Null => allowed.Contains("null"),
        _ => false,
    };

    private static bool JsonEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            return false;
        }

        return a.ValueKind switch
        {
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Null => true,
            _ => a.GetRawText() == b.GetRawText(),
        };
    }

    private static string SchemaPath()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "scaffold-dotnet.slnx")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("repo root not found"),
            "schemas", "cli-envelope.schema.json");
    }
}
