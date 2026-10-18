using System.Text.Json;
using BDTest.Maps;

namespace BDTest.Helpers;

// Historical Newtonsoft reports put $id on the root and may reference earlier objects.
// Expand those references only when reading; current writers always emit plain values.
internal static class LegacyReportJson
{
    internal static BDTestOutputModel? Deserialize(JsonElement root, long maximumExpandedSize)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("$id", out _))
        {
            return root.Deserialize(BDTestJsonContext.Default.BDTestOutputModel);
        }

        var references = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        Index(root, references);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            Write(root, writer, references, new HashSet<string>(StringComparer.Ordinal), 0, maximumExpandedSize);
        }
        buffer.Position = 0;
        return JsonSerializer.Deserialize(buffer, BDTestJsonContext.Default.BDTestOutputModel);
    }

    private static void Index(JsonElement element, Dictionary<string, JsonElement> references)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("$id", out var id))
            {
                var referenceId = id.ValueKind == JsonValueKind.String ? id.GetString() : null;
                if (referenceId == null || references.ContainsKey(referenceId))
                {
                    throw new JsonException("Invalid or duplicate legacy reference id.");
                }
                references.Add(referenceId, element);
            }
            foreach (var property in element.EnumerateObject())
            {
                Index(property.Value, references);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                Index(item, references);
            }
        }
    }

    private static void Write(JsonElement element, Utf8JsonWriter writer,
        Dictionary<string, JsonElement> references, HashSet<string> activeReferences, int depth, long maximumExpandedSize)
    {
        if (depth > 64)
        {
            throw new JsonException("Legacy report exceeds the maximum JSON depth.");
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            // A dictionary can contain these names alongside its own $id and ordinary entries.
            var propertyCount = element.EnumerateObject().Count();
            if (propertyCount == 1 && element.TryGetProperty("$ref", out var reference))
            {
                var referenceId = reference.ValueKind == JsonValueKind.String ? reference.GetString() : null;
                if (referenceId == null
                    || !references.TryGetValue(referenceId, out var target)
                    || !activeReferences.Add(referenceId))
                {
                    throw new JsonException("Unknown or cyclic legacy report reference.");
                }
                Write(target, writer, references, activeReferences, depth + 1, maximumExpandedSize);
                activeReferences.Remove(referenceId);
                return;
            }
            if (element.TryGetProperty("$values", out var values)
                && values.ValueKind == JsonValueKind.Array
                && propertyCount == (element.TryGetProperty("$id", out _) ? 2 : 1))
            {
                Write(values, writer, references, activeReferences, depth + 1, maximumExpandedSize);
                return;
            }
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name == "$id")
                {
                    continue;
                }
                writer.WritePropertyName(property.Name);
                Write(property.Value, writer, references, activeReferences, depth + 1, maximumExpandedSize);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
            {
                Write(item, writer, references, activeReferences, depth + 1, maximumExpandedSize);
            }
            writer.WriteEndArray();
        }
        else
        {
            element.WriteTo(writer);
        }
        if (writer.BytesCommitted + writer.BytesPending > maximumExpandedSize)
        {
            throw new JsonException("Expanded legacy report exceeds the upload size limit.");
        }
    }
}
