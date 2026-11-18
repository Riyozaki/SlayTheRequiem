using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SlayTheRequiem.SlayTheRequiemCode.Core;

/// <summary>Wire/save identities use shared saved facts, never DateTime.Now, process RNG or culture-sensitive strings.</summary>
public static class OperationIdentity
{
    public static string Hash(string data) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data)));
    public static string RunKey(string seed, string? nonce, ulong owner) => string.IsNullOrEmpty(nonce) ? ""
        : Hash(seed + "|" + nonce + "|" + owner.ToString(CultureInfo.InvariantCulture));
    public static string LegacyNonce(string seed, long savedStartTime) =>
        Hash("legacy|" + seed + "|" + savedStartTime.ToString(CultureInfo.InvariantCulture));
    public static string Id(string run, int revision) => run + ":" + revision.ToString(CultureInfo.InvariantCulture);
    public static bool IsDigest(string? text) => text is { Length: 64 } && text.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');

    public static bool TryRevision(string run, string? id, out int revision)
    {
        revision = -1;
        if (!IsDigest(run) || id == null || !id.StartsWith(run + ":", StringComparison.Ordinal)) return false;
        var tail = id[(run.Length + 1)..];
        return int.TryParse(tail, NumberStyles.None, CultureInfo.InvariantCulture, out revision) && revision >= 0
            && tail == revision.ToString(CultureInfo.InvariantCulture);
    }
}
