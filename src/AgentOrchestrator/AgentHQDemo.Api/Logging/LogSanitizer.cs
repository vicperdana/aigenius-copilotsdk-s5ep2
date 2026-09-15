using System.Text.RegularExpressions;

namespace AgentHQDemo.Api.Logging;

/// <summary>
/// Makes untrusted values safe to write into a log.
/// </summary>
/// <remarks>
/// A prompt or model name arrives straight from the request body, so a caller can
/// embed newlines and forge convincing extra log entries — the attack CodeQL calls
/// <c>cs/log-forging</c>. Collapsing every Unicode control character removes the
/// line breaks that make a forged entry look real, along with the terminal escape
/// sequences that could reformat a console reading the log.
/// <para>
/// Mirrors <c>app/log_sanitizer.py</c> in the Python track.
/// </para>
/// </remarks>
public static partial class LogSanitizer
{
    private const int DefaultMaxLength = 200;

    [GeneratedRegex(@"\p{C}+")]
    private static partial Regex ControlCharacters();

    /// <summary>
    /// Replaces runs of control characters in <paramref name="value"/> with a single
    /// space and truncates the result to <paramref name="maxLength"/>, so one request
    /// cannot flood the log.
    /// </summary>
    public static string Sanitize(string? value, int maxLength = DefaultMaxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";

        var cleaned = ControlCharacters().Replace(value, " ");
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength] + "…";
    }
}
