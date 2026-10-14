using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
namespace Kohami.M0.Security;

public static class Totp
{
    const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    public static string Encode(byte[] bytes) { var result = new StringBuilder(); int buffer = 0, bits = 0; foreach (var value in bytes) { buffer = (buffer << 8) | value; bits += 8; while (bits >= 5) { bits -= 5; result.Append(Alphabet[(buffer >> bits) & 31]); } } if (bits > 0) result.Append(Alphabet[(buffer << (5 - bits)) & 31]); return result.ToString(); }
    public static byte[] Decode(string value) { var result = new List<byte>(); int buffer = 0, bits = 0; foreach (var c in value) { var n = Alphabet.IndexOf(c); if (n < 0) throw new FormatException("BASE32_INVALID"); buffer = (buffer << 5) | n; bits += 5; if (bits >= 8) { bits -= 8; result.Add((byte)(buffer >> bits)); } } return result.ToArray(); }
    public static string Code(byte[] seed, long step) { Span<byte> counter = stackalloc byte[8]; BinaryPrimitives.WriteInt64BigEndian(counter, step); var digest = HMACSHA1.HashData(seed, counter); var offset = digest[^1] & 15; var n = BinaryPrimitives.ReadUInt32BigEndian(digest.AsSpan(offset, 4)) & 0x7fffffff; return (n % 1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture); }
    public static long? Match(byte[] seed, string code, DateTimeOffset now, long lastAccepted) { if (code.Length != 6 || code.Any(c => c is < '0' or > '9')) return null; var step = now.ToUnixTimeSeconds() / 30; foreach (var match in new[] { step, step - 1, step + 1 }) if (match > lastAccepted && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(code), Encoding.ASCII.GetBytes(Code(seed, match)))) return match; return null; }
}
