namespace feitoapi.api.Services;

/// <summary>
/// Validates and decodes a user-supplied base64 logo. Only PNG/JPEG magic bytes are
/// accepted, and the decoded size is capped to prevent memory-abuse via huge payloads.
/// </summary>
public static class ImageValidator
{
    private const int MaxDecodedBytes = 1_000_000; // ~1 MB

    private static readonly byte[] PngMagic = { 0x89, 0x50, 0x4E, 0x47 };
    private static readonly byte[] JpegMagic = { 0xFF, 0xD8, 0xFF };

    public enum Result { Ok, None, TooLarge, InvalidBase64, UnsupportedFormat }

    public static Result TryDecode(string? base64, out byte[]? bytes)
    {
        bytes = null;
        if (string.IsNullOrWhiteSpace(base64))
            return Result.None;

        // Strip an optional data-URI prefix like "data:image/png;base64,"
        var comma = base64.IndexOf(',');
        if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma >= 0)
            base64 = base64[(comma + 1)..];

        // Cheap size guard before allocating: base64 is ~4/3 of the decoded size.
        if ((long)base64.Length * 3 / 4 > MaxDecodedBytes + 1024)
            return Result.TooLarge;

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(base64.Trim());
        }
        catch (FormatException)
        {
            return Result.InvalidBase64;
        }

        if (decoded.Length > MaxDecodedBytes)
            return Result.TooLarge;

        if (!StartsWith(decoded, PngMagic) && !StartsWith(decoded, JpegMagic))
            return Result.UnsupportedFormat;

        bytes = decoded;
        return Result.Ok;
    }

    private static bool StartsWith(byte[] data, byte[] prefix)
    {
        if (data.Length < prefix.Length) return false;
        for (int i = 0; i < prefix.Length; i++)
            if (data[i] != prefix[i]) return false;
        return true;
    }
}
