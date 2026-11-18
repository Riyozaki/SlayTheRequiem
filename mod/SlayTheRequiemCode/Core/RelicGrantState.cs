using System.Globalization;
using System.Text.Json;

namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>
/// A companion relic belongs to an issuer, not to a search for an item class. Persist the exact entitlement before
/// the issuer's first callback; never replay its one-shot effects. Completed rights survive later sale/loss/storage.
/// </summary>
public sealed record RelicGrantState(int Version, int Phase, string Key, string Payload)
{
    public const int CurrentVersion = 1, Prepared = 0, Ready = 1, Completed = 2;
    public const int MaximumLength = 2 * 1024 * 1024;

    public static RelicGrantState Prepare(string key, string payload)
    {
        var value = new RelicGrantState(CurrentVersion, Prepared, key, payload);
        value.Validate();
        return value;
    }

    public RelicGrantState IssuerCompleted() => Phase == Prepared ? this with { Phase = Ready } : this;
    public RelicGrantState Complete() => Phase is Ready or Completed ? this with { Phase = Completed }
        : throw new InvalidOperationException("Issuer callback has no confirmed result; restore its safe checkpoint, do not replay it");

    public string Encode()
    {
        Validate();
        var text = JsonSerializer.Serialize(this);
        if (text.Length > MaximumLength) throw new FormatException("Companion entitlement exceeds the safety limit");
        return text;
    }

    /// <summary>Empty is a legacy source with NO proof of non-delivery, not permission to grant another item.</summary>
    public static RelicGrantState? Read(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        if (text.Length > MaximumLength) throw new FormatException("Companion entitlement exceeds the safety limit");
        try
        {
            var value = JsonSerializer.Deserialize<RelicGrantState>(text) ?? throw new FormatException("Empty companion entitlement");
            value.Validate();
            if (!SavedPayload.Equivalent(text, value.Encode())) throw new FormatException("Ambiguous or unknown companion entitlement fields; original retained");
            return value;
        }
        catch (JsonException e) { throw new FormatException("Invalid companion entitlement; original retained", e); }
    }

    private void Validate()
    {
        if (Version != CurrentVersion || Phase is < Prepared or > Completed || Key == null || Payload == null || Payload.Length > MaximumLength / 2)
            throw new FormatException("Unsupported companion entitlement; original retained");
        if (Key.Length == 0 && Payload.Length == 0) return; // a path such as Reborn deliberately has no sign
        if (!Key.StartsWith("r:", StringComparison.Ordinal)
            || !int.TryParse(Key.AsSpan(2), NumberStyles.None, CultureInfo.InvariantCulture, out var sequence) || sequence <= 0
            || Key != "r:" + sequence.ToString(CultureInfo.InvariantCulture) || Payload.Length == 0)
            throw new FormatException("Invalid companion instance identity; original retained");
        using var doc = JsonDocument.Parse(Payload);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new FormatException("Invalid companion relic payload; original retained");
    }
}
