"""Makes untrusted values safe to write into a log.

A prompt or model name arrives straight from the request body, so a caller can
embed newlines and forge convincing extra log entries — the attack CodeQL calls
``cs/log-forging`` on the .NET side. Collapsing every Unicode control character
removes the line breaks that make a forged entry look real, along with the
terminal escape sequences that could reformat a console reading the log.

Mirrors ``AgentHQDemo.Api/Logging/LogSanitizer.cs`` in the .NET track. ``re`` has
no ``\\p{C}`` class, so the category is tested per character instead.
"""

import unicodedata
from itertools import groupby

DEFAULT_MAX_LENGTH = 200


def _is_control(char: str) -> bool:
    """Whether ``char`` is in Unicode category C — the .NET ``\\p{C}`` class."""
    return unicodedata.category(char).startswith("C")


def sanitize(value: str | None, max_length: int = DEFAULT_MAX_LENGTH) -> str:
    """Replace runs of control characters with a single space and truncate.

    Truncation stops a single request from flooding the log.
    """
    if not value:
        return ""

    cleaned = "".join(
        " " if is_control else "".join(chars)
        for is_control, chars in groupby(value, key=_is_control)
    )
    return cleaned if len(cleaned) <= max_length else cleaned[:max_length] + "…"
