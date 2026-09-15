"""Guards the model picker against leaking internal-only models.

Accounts with internal entitlements see models named like
``GPT-5.6 Sol Fast (Internal only)``. The picker is populated from the live CLI
list, so without a filter those names appear on screen during a demo or stream.

Mirrors ``tests/AgentHQDemo.Tests/ModelVisibilityTests.cs`` in the .NET track.
Plain tests rather than ``parametrize`` so both suites report the same count.
"""

from app.routers.chat import AVAILABLE_MODELS, _is_internal_only


def test_flags_internal_models_regardless_of_case():
    """The marker is matched case-insensitively — the CLI's casing is not a contract."""
    for name in (
        "GPT-5.6 Sol Fast (Internal only)",
        "GPT-5.6 Sol Fast (INTERNAL ONLY)",
        "gpt-5.6 sol fast (internal only)",
    ):
        assert _is_internal_only(name), f"'{name}' should be hidden."


def test_does_not_flag_models_we_actually_ship():
    """An over-broad pattern would silently hide a real model.

    Pin the whole static catalogue rather than a single example.
    """
    for model in AVAILABLE_MODELS.values():
        assert not _is_internal_only(model.name), (
            f"'{model.name}' is a real model but was filtered out of the picker."
        )


def test_treats_missing_names_as_visible():
    """A missing name is not evidence of an internal model."""
    assert not _is_internal_only(None)
    assert not _is_internal_only("")
