using System;
using System.Text.Json;

namespace EmberLighthouse.Networking;

/// <summary>Optional small versioned JSON envelope, independent of game state.</summary>
public static class Protocol
{
    public const int Version = 1;

    public static string Encode<T>(string kind, T body, long sequence = 0) =>
        JsonSerializer.Serialize(new { version = Version, kind, sequence, body });

    /// <summary>The returned body is cloned and remains valid after parsing finishes.</summary>
    public static bool TryDecode(string payload, out string kind, out JsonElement body, out long sequence)
    {
        kind = ""; body = default; sequence = 0;
        if (string.IsNullOrWhiteSpace(payload) || payload.Length > NetworkSession.MaximumReliableBytes) return false;
        try
        {
            using var document = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 48 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("version", out var version) || !version.TryGetInt32(out int value) || value != Version ||
                !root.TryGetProperty("kind", out var name) || name.ValueKind != JsonValueKind.String ||
                !root.TryGetProperty("body", out var content)) return false;
            kind = name.GetString() ?? "";
            if (kind.Length is < 1 or > 48) return false;
            if (root.TryGetProperty("sequence", out var number) && !number.TryGetInt64(out sequence)) return false;
            body = content.Clone();
            return true;
        }
        catch (JsonException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}
