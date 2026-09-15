namespace AgentHQDemo.Api.Services;

/// <summary>
/// Helpers for writing user-supplied values to logs safely.
/// </summary>
internal static class LogSanitizer
{
    /// <summary>
    /// Removes line breaks from a user-supplied value so it cannot be used to
    /// forge additional log entries (CWE-117).
    /// </summary>
    public static string Sanitize(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Replace("\r", string.Empty, StringComparison.Ordinal)
                    .Replace("\n", string.Empty, StringComparison.Ordinal);
    }
}
