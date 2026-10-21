namespace Kohami.Native;

public static class SafeDiagnostic
{
    public static string? ReasonCode(string? message)
    {
        if (string.IsNullOrEmpty(message)) return null;

        var separator = message.IndexOf(':');
        var candidate = separator < 0 ? message : message[..separator];
        if (candidate.Length is < 3 or > 80 || !char.IsAsciiLetterUpper(candidate[0])) return null;
        if (candidate.Any(character => !char.IsAsciiLetterUpper(character) && !char.IsAsciiDigit(character) && character != '_'))
            return null;

        return candidate;
    }
}
