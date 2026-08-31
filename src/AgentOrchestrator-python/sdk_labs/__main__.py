"""Runnable samples backing the Copilot SDK labs in docs/labs-python/.

Each subcommand maps to one lab.
"""

import argparse
import asyncio
import sys

USAGE = """\
sdk_labs — runnable samples for the Copilot SDK labs.

Usage:
  uv run python -m sdk_labs <command> [--model <id>]

Commands:
  tools         Lab 03 — define a tool the model can call
  events        Lab 04 — observe the session event lifecycle
  sessions      Lab 05 — persist and resume a session
  mcp           Lab 06 — attach an MCP server

Diagnostic (no lab):
  permissions   Reference code for on_permission_request.
                Observed here: the handler fires for custom tools but
                NOT for shell commands, because the host CLI already
                grants shell approval. Treat it as a starting point to
                test in your own environment, not as a working security
                control. See docs/labs-python/03-tools/.
"""

COMMANDS = ("tools", "events", "sessions", "mcp", "permissions", "codealong", "codealong-final")


async def _dispatch(command: str, model: str | None, resume: str | None, phase: int = 0) -> int:
    # Imported lazily so `--help` does not pay for SDK import time.
    if command == "tools":
        from sdk_labs import tools_sample

        return await tools_sample.run(model)
    if command == "events":
        from sdk_labs import events_sample

        return await events_sample.run(model)
    if command == "sessions":
        from sdk_labs import sessions_sample

        return await sessions_sample.run(model, resume)
    if command == "mcp":
        from sdk_labs import mcp_sample

        return await mcp_sample.run(model)
    if command in ("codealong", "codealong-final"):
        import importlib

        mod = "code_along" if command == "codealong" else "code_along_final"
        ca = importlib.import_module(f"sdk_labs.{mod}")
        return await ca.run(phase, model)

    if command == "permissions":
        from sdk_labs import permissions_sample

        return await permissions_sample.run(model)
    return 1


def main() -> int:
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("command", nargs="?", choices=COMMANDS)
    parser.add_argument("--model")
    parser.add_argument("--resume")
    parser.add_argument("--phase", type=int, default=0)

    try:
        args, unknown = parser.parse_known_args()
    except SystemExit:
        print(USAGE)
        return 1

    if args.command is None or unknown:
        if unknown:
            print(f"Unknown or incomplete option: {' '.join(unknown)}")
        print(USAGE)
        return 1

    return asyncio.run(_dispatch(args.command, args.model, args.resume, args.phase))


if __name__ == "__main__":
    sys.exit(main())
