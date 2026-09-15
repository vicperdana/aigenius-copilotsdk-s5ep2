"""Helpers for writing user-supplied values to logs safely.

Mirrors ``AgentHQDemo.Api/Services/LogSanitizer.cs``.
"""


def sanitize_for_log(value: str | None) -> str:
    """Removes line breaks from a user-supplied value so it cannot be used to
    forge additional log entries (CWE-117).
    """
    if not value:
        return ""

    return value.replace("\r", "").replace("\n", "")
